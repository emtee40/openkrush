﻿using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.Kknd.Traits.Production
{
	public enum SpawnType { PlaceBuilding, Deploy, Other }

	public class SelfConstructingInfo : WithMakeAnimationInfo, ITraitInfo, Requires<ConditionManagerInfo>, Requires<IHealthInfo>
	{
		[Desc("Number of make sequences.")]
		public readonly int Steps = 3;

		public new object Create(ActorInitializer init) { return new SelfConstructing(init, this); }
	}

	public class SelfConstructing : WithMakeAnimation, ITick, INotifyRemovedFromWorld, INotifyCreated, INotifyDamageStateChanged, INotifyKilled
	{
		private readonly SelfConstructingInfo Info;

		private readonly WithSpriteBody wsb;

		private readonly ConditionManager conditionManager;
		private int token = ConditionManager.InvalidConditionToken;

		private ProductionItem productionItem;

		private List<int> healthSteps;
		private Health health;
		private int step;
		private SpawnType spawnType;

		public SelfConstructing(ActorInitializer init, SelfConstructingInfo info) : base(init, info)
		{
			Info = info;
			wsb = init.Self.Trait<WithSpriteBody>();
			conditionManager = init.Self.Trait<ConditionManager>();

			if (!string.IsNullOrEmpty(Info.Condition) && token == ConditionManager.InvalidConditionToken)
				token = conditionManager.GrantCondition(init.Self, Info.Condition);
			
			spawnType = init.Contains<PlaceBuildingInit>() ? SpawnType.PlaceBuilding : init.Contains<SpawnedByMapInit>() ? SpawnType.Other : SpawnType.Deploy;
		}

		void INotifyCreated.Created(Actor self)
		{
			if (spawnType == SpawnType.PlaceBuilding)
			{
				var productionQueue = self.Owner.PlayerActor.TraitsImplementing<SelfConstructingProductionQueue>().First(q => q.AllItems().Contains(self.Info));
				var valued = self.Info.TraitInfoOrDefault<ValuedInfo>();
				productionItem = new SelfConstructingProductionItem(productionQueue, self, valued == null ? 0 : valued.Cost, null, null);
				productionQueue.BeginProduction(productionItem, false);

				health = self.Trait<Health>();

				healthSteps = new List<int>();
				for (var i = 0; i <= Info.Steps; i++)
					healthSteps.Add(health.MaxHP * (i + 1) / (Info.Steps + 1));

				health.InflictDamage(self, self, new Damage(health.MaxHP - healthSteps[0]), true);

				wsb.CancelCustomAnimation(self);
				wsb.PlayCustomAnimationRepeating(self, Info.Sequence + 0);
			}
			else if (spawnType == SpawnType.Deploy)
			{
				wsb.CancelCustomAnimation(self);
				wsb.PlayCustomAnimation(self, "deploy", () => OnComplete(self));
			}
			else
				OnComplete(self);
		}

		private void OnComplete(Actor self)
		{
			if (token != ConditionManager.InvalidConditionToken)
				token = conditionManager.RevokeCondition(self, token);
		}

		void ITick.Tick(Actor self)
		{
			if (productionItem == null)
				return;

			if (productionItem.Done)
			{
				productionItem.Queue.EndProduction(productionItem);
				productionItem = null;
				wsb.CancelCustomAnimation(self);

				while (step < Info.Steps)
					health.InflictDamage(self, self, new Damage(healthSteps[step] - healthSteps[++step]), true);

				OnComplete(self);
				return;
			}

			var progress = Math.Max(0, Math.Min(Info.Steps * (productionItem.TotalTime - productionItem.RemainingTime) / Math.Max(1, productionItem.TotalTime), Info.Steps - 1));

			if (progress != step)
			{
				while (step < progress)
					health.InflictDamage(self, self, new Damage(healthSteps[step] - healthSteps[++step]), true);

				wsb.PlayCustomAnimationRepeating(self, Info.Sequence + step);
			}
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			if (productionItem != null)
				productionItem.Queue.EndProduction(productionItem);
		}

		public ProductionItem TryAbort(Actor self)
		{
			if (productionItem == null)
				return null;

			var item = productionItem;

			productionItem.Queue.EndProduction(productionItem);
			productionItem = null;
			OnComplete(self);

			return item;
		}

		void INotifyDamageStateChanged.DamageStateChanged(Actor self, AttackInfo e)
		{
			if (productionItem == null)
				return;

			wsb.PlayCustomAnimationRepeating(self, Info.Sequence + step);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (productionItem != null)
				productionItem.Queue.EndProduction(productionItem);
		}
	}
}
