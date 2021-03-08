#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenKrush Developers (see AUTHORS)
 * This file is part of OpenKrush, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.OpenKrush.FileFormats;
using OpenRA.Mods.OpenKrush.FileSystem;
using OpenRA.Mods.OpenKrush.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenKrush.SpriteLoaders
{
	public class MobdLoader : ISpriteLoader
	{
		private class MobdSpriteFrame : ISpriteFrame
		{
			public SpriteFrameType Type => SpriteFrameType.Indexed8;
			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; private set; }
			public byte[] Data { get; private set; }
			public readonly uint[] Palette;
			public readonly MobdPoint[] Points;

			public bool DisableExportPadding => true;

			public MobdSpriteFrame(MobdFrame mobdFrame)
			{
				var width = mobdFrame.RenderFlags.Image.Width;
				var height = mobdFrame.RenderFlags.Image.Height;
				var x = mobdFrame.OriginX;
				var y = mobdFrame.OriginY;

				Size = new Size((int)width, (int)height);
				FrameSize = new Size((int)width, (int)height);
				Offset = new int2((int)(width / 2 - x), (int)(height / 2 - y));
				Data = mobdFrame.RenderFlags.Image.Pixels;
				Palette = mobdFrame.RenderFlags.Palette;
				Points = mobdFrame.Points;
			}
		}

		private static bool IsMobd(Stream stream, out Generation generation)
		{
			generation = Generation.Unknown;

			if (!(stream is SegmentStream innerStream))
				return false;

			if (!(innerStream.BaseStream is SegmentStream outerStream))
				return false;

			var originalPosition = outerStream.BaseStream.Position;
			outerStream.BaseStream.Position = 0;
			var magic = outerStream.BaseStream.ReadASCII(4);
			outerStream.BaseStream.Position = originalPosition;

			switch (magic)
			{
				case "DATA":
					generation = Generation.Gen1;
					return true;

				case "DAT2":
					generation = Generation.Gen2;
					return true;

				default:
					return false;
			}
		}

		public bool TryParseSprite(Stream stream, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			Generation generation;

			if (!IsMobd(stream, out generation))
			{
				metadata = null;
				frames = null;
				return false;
			}

			var mobd = new Mobd(stream as SegmentStream, generation);
			var tmp = new List<MobdSpriteFrame>();

			tmp.AddRange(from mobdAnimation in mobd.RotationalAnimations
				from mobdFrame in mobdAnimation.Frames
				select new MobdSpriteFrame(mobdFrame));
			tmp.AddRange(from mobdAnimation in mobd.SimpleAnimations
				from mobdFrame in mobdAnimation.Frames
				select new MobdSpriteFrame(mobdFrame));

			uint[] palette = null;
			var points = new Dictionary<int, Offset[]>();

			for (var i = 0; i < tmp.Count; i++)
			{
				if (tmp[i].Points != null)
					points.Add(i, tmp[i].Points.Select(point => new Offset(point.Id, point.X, point.Y)).ToArray());

				if (tmp[i].Palette != null)
					palette = tmp[i].Palette;
			}

			frames = tmp.ToArray();

			metadata = new TypeDictionary { new EmbeddedSpritePalette(palette), new EmbeddedSpriteOffsets(points) };

			return true;
		}
	}
}