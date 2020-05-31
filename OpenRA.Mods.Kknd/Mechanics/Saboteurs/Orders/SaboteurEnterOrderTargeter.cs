#region Copyright & License Information

/*
 * Copyright 2016-2020 The KKnD Developers (see AUTHORS)
 * This file is part of KKnD, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Mods.Common.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Kknd.Mechanics.Saboteurs.Orders
{
	class SaboteurEnterOrderTargeter : UnitOrderTargeter
	{
		public const string Id = "SaboteurEnter";

		private readonly string cursorAllowed;
		private readonly string cursorForbidden;

		public SaboteurEnterOrderTargeter(string cursorAllowed, string cursorForbidden)
			: base(Id, 7, null, false, true)
		{
			this.cursorAllowed = cursorAllowed;
			this.cursorForbidden = cursorForbidden;
		}

		public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
		{
			if (SaboteurUtils.CanEnter(self, target))
			{
				cursor = cursorAllowed;
				return true;
			}

			cursor = cursorForbidden;
			return false;
		}

		public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
		{
			return CanTargetActor(self, target.Actor, modifiers, ref cursor);
		}
	}
}
