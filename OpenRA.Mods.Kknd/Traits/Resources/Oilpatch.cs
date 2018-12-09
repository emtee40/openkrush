using System;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Kknd.Traits.Resources
{
	[Desc("KKnD specific oilpatch implementation.")]
	class OilpatchInfo : IRulesetLoaded, IHealthInfo, Requires<ConditionManagerInfo>
	{
		[Desc("How many oil will be burned per tick.")]
		public readonly int BurnAmount = 5;

		[Desc("Amount of oil on spawn. Use -1 for infinite")]
		public readonly int Amount = 0;

		[Desc("Amount of oil for a full oilpatch.")]
		public readonly int FullAmount = 100000;

		[GrantedConditionReference]
		[Desc("Condition to grant while this actor is burning.")]
		public readonly string Condition = "Burning";

		[WeaponReference]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = "oilburn";

		public int MaxHP { get { return FullAmount; } }

		public WeaponInfo WeaponInfo { get; private set; }

		public object Create(ActorInitializer init) { return new Oilpatch(init, this); }
		
		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (!string.IsNullOrEmpty(Weapon))
			{
				WeaponInfo weapon;
				var weaponToLower = Weapon.ToLowerInvariant();
				if (!rules.Weapons.TryGetValue(weaponToLower, out weapon))
					throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(weaponToLower));
				WeaponInfo = weapon;
			}
		}
	}

	class Oilpatch : IHealth, ITick, IHaveOil
	{
		private readonly OilpatchInfo info;

		private int resources;

		public DamageState DamageState { get { return DamageState.Undamaged; } }
		public int HP { get { return resources == -1 ? info.FullAmount : Math.Min(resources, info.FullAmount); } }
		public int MaxHP { get { return info.FullAmount; } }
		public int DisplayHP { get { return HP; } }
		public bool IsDead { get { return resources == 0; } }

		private readonly ConditionManager conditionManager;
		private int token = ConditionManager.InvalidConditionToken;

		private int burnTotal;
		private int burnLeft;

		public Oilpatch(ActorInitializer init, OilpatchInfo info)
		{
			this.info = info;
			resources = info.Amount == 0 ? init.World.WorldActor.Trait<OilAmount>().Amount : info.Amount;
			burnTotal = this.info.FullAmount * init.World.WorldActor.Trait<OilpatchBurn>().Amount / 100;
			conditionManager = init.Self.TraitOrDefault<ConditionManager>();
		}

		public int Current { get { return resources == -1 ? info.FullAmount : resources; } }
		public int Maximum { get { return info.FullAmount; } }

		public int Pull(int amount)
		{
			if (resources == -1)
				return amount;

			var pullAmount = Math.Min(amount, Current);
			resources -= pullAmount;
			return pullAmount;
		}

		public int Push(int amount)
		{
			if (resources != -1)
				resources += amount;

			return 0;
		}

		void ITick.Tick(Actor self)
		{
			if (token != ConditionManager.InvalidConditionToken)
			{
				if (!self.IsInWorld)
				{
					conditionManager.RevokeCondition(self, token);
					token = ConditionManager.InvalidConditionToken;
					burnLeft = 0;
				}
				else
				{
					var damage = Math.Min(info.BurnAmount, burnLeft);
					burnLeft -= damage;
					
					if (resources > 0)
						resources -= Math.Min(resources, damage);
				
					if (burnLeft == 0)
					{
						conditionManager.RevokeCondition(self, token);
						token = ConditionManager.InvalidConditionToken;
					}
					
					info.WeaponInfo.Impact(Target.FromPos(self.CenterPosition), self, Enumerable.Empty<int>());
				}
			}

			if (resources == 0)
				self.Dispose();
		}

		void IHealth.InflictDamage(Actor self, Actor attacker, Damage damage, bool ignoreModifiers)
		{
			if (burnTotal == 0 || info.BurnAmount == 0 || damage.Value <= 0 || attacker == self)
				return;

			burnLeft = burnTotal;

			if (!string.IsNullOrEmpty(info.Condition) && token == ConditionManager.InvalidConditionToken)
				token = conditionManager.GrantCondition(self, info.Condition);
		}

		void IHealth.Kill(Actor self, Actor attacker, BitSet<DamageType> damageTypes)
		{
			resources = 0;
		}
	}
}
