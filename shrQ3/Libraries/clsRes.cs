#pragma warning disable CA1416, IDE1006

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

namespace shrQ3
{
    public sealed class clsRes
    {
        private static readonly string[] _languages = [string.Empty, "F_", "G_"];
        private static readonly string[] _regions = ["r", "fleet", "form"];
        private static readonly string[] _races = ["FF", "GG", "HH", "II", "KK", "LL", "RR", "ZZ", "PP"];

        public static bool SetResolution(string spritesPath, int width, int height, List<Size> resolutions, double alignment)
        {
            FileStream f = null;
            BinaryReader r = null;
            BinaryWriter w = null;

            try
            {
                f = new(spritesPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                r = new(f, Encoding.UTF8, leaveOpen: true);
                w = new(f, Encoding.UTF8, leaveOpen: true);

                clsQ3 q3 = new();

                q3.Load(r, clsQ3.IndexingMethods.FileOffset);
                q3.Update();

                if (resolutions == null)
                {
                    if ((width == 640 && height == 480) || (width == 800 && height == 600))
                    {
                        ReplaceIntro(r, w, q3, width, width, height);
                        CenterUIs(r, w, q3, width, height);
                    }
                    else if (width > 800 && height >= 600)
                    {
                        SortedDictionary<int, Size> d = [];

                        GetResolutions(q3, d);

                        if (!d.TryGetValue(width, out Size s))
                        {
                            foreach (KeyValuePair<int, Size> p in d)
                            {
                                s = p.Value;

                                Contract.Assert(s.Width != 0 && s.Height != 0);

                                if (s.Width >= width)
                                    break;
                            }
                        }

                        if (ReplaceResolution(r, w, q3, 800, 600, s.Width, s.Height, width, height, alignment))
                        {
                            ReplaceIntro(r, w, q3, s.Width, width, height);
                            CenterUIs(r, w, q3, width, height);
                        }
                    }
                    else
                        throw new NotSupportedException();
                }
                else
                {
                    SortedDictionary<int, Size> d = [];

                    GetResolutions(q3, d);

                    if (resolutions.Count != d.Count)
                        throw new NotSupportedException();

                    // creates an ordered list with the old resolutions

                    List<Size> oldResolutions = new(d.Count);

                    foreach (KeyValuePair<int, Size> p in d)
                    {
                        Size s = p.Value;

                        Contract.Assert(s.Width != 0 && s.Height != 0);

                        oldResolutions.Add(s);
                    }

                    d.Clear();

                    // creates an ordered list with the new resolutions

                    for (int i = 0; i < resolutions.Count; i++)
                        d.Add(resolutions[i].Width, resolutions[i]);

                    resolutions.Clear();

                    foreach (KeyValuePair<int, Size> p in d)
                        resolutions.Add(p.Value);

                    d.Clear();

                    // tries to replace all the old resolutions (in reverse order)

                    for (int i = oldResolutions.Count - 1; i >= 0; i--)
                    {
                        if (!ReplaceResolution(r, w, q3, 800, 600, oldResolutions[i].Width, oldResolutions[i].Height, resolutions[i].Width, resolutions[i].Height, alignment))
                            throw new NotSupportedException();
                    }

                    // we assume the new lowest resolution will be the new default

                    ReplaceIntro(r, w, q3, oldResolutions[0].Width, resolutions[0].Width, resolutions[0].Height);
                    CenterUIs(r, w, q3, resolutions[0].Width, resolutions[0].Height);
                }

                w.Seek(0, SeekOrigin.Begin);

                q3.Save(w, clsQ3.IndexingMethods.FileOffset);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                w?.Dispose();
                r?.Dispose();
                f?.Dispose();
            }
        }

        private static void GetResolutions(clsQ3 q3, SortedDictionary<int, Size> d)
        {
            foreach (KeyValuePair<int, tAsset> p in q3.Assets)
            {
                string t = p.Value.Name.Value;

                if (t.StartsWith('r'))
                {
                    int i = t.IndexOf('x');

                    if (i >= 4)
                    {
                        Size s = new(int.Parse(t[1..i], NumberStyles.None, CultureInfo.InvariantCulture), int.Parse(t[(i + 1)..], NumberStyles.None, CultureInfo.InvariantCulture));

                        if (s.Width > 800 && s.Height >= 600)
                            d.Add(s.Width, s);
                    }
                }
            }
        }

        private static bool ReplaceResolution(BinaryReader r, BinaryWriter w, clsQ3 q3, int refWidth, int refHeight, int oldWidth, int oldHeight, int newWidth, int newHeight, double alignment)
        {
            Contract.Assert(r != null && w != null);

            int refX1, refX2;
            int refY;

            if (q3.TryGetAsset($"r{refWidth}x{refHeight}", out tAsset asset1))
            {
                tGroupCell cell = ((tVisualGroupAsset)asset1).Cells[4];

                refX1 = cell.X;
                refX2 = 0;

                refY = cell.Y;

                if (q3.TryGetAsset($"form{refWidth}x{refHeight}", out asset1))
                    refX2 = ((tGenericRectAsset)q3.Assets[((tVisualGroupAsset)asset1).Cells[0].Id]).Extent.X;

                Contract.Assert(refX2 != 0);
            }
            else
            {
                // this isn't a standard file

                return false;
            }

            for (int i = 0; i < _languages.Length; i++)
            {
                if (!q3.TryGetAsset($"{_languages[i]}ViewportScene_{oldWidth}", out asset1))
                    continue;

                tVisualGroupAsset curScene = (tVisualGroupAsset)asset1;

                curScene.Name.Value = $"{_languages[i]}ViewportScene_{newWidth}";

                tGroupCell cell = curScene.Cells[0];

                cell.X = newWidth >> 1;
                cell.Y = newHeight >> 1;
            }

            for (int i = 0; i < _regions.Length; i++)
            {
                if (
                    !q3.TryGetAsset($"{_regions[i]}{refWidth}x{refHeight}", out asset1) ||
                    !q3.TryGetAsset($"{_regions[i]}{oldWidth}x{oldHeight}", out tAsset asset2)
                )
                    continue;

                tVisualGroupAsset refScene = (tVisualGroupAsset)asset1;
                tVisualGroupAsset curScene = (tVisualGroupAsset)asset2;

                curScene.Name.Value = $"{_regions[i]}{newWidth}x{newHeight}";

                double curSpace = (newWidth - refWidth) * alignment;

                int space2 = (int)Math.Round(curSpace / 2.0, MidpointRounding.AwayFromZero);
                int space3 = (int)Math.Round(curSpace / 3.0, MidpointRounding.AwayFromZero);

                int refCenter = refWidth - refX1 >> 1;
                int curCenter = newWidth - refX1 >> 1;
                int newCenter = curCenter - refCenter - space2;

                for (int j = 0; j < curScene.Cells.Length; j++)
                {
                    tGroupCell refCell = refScene.Cells[j];
                    tGroupCell curCell = curScene.Cells[j];

                    tGenericRectAsset refRect = (tGenericRectAsset)q3.Assets[refCell.Id];
                    tGenericRectAsset curRect = (tGenericRectAsset)q3.Assets[curCell.Id];

                    switch (i)
                    {
                        case 0: // r
                            switch (j)
                            {
                                case 4:
                                    {
                                        curRect.Name.Value = $"t%viewport{newWidth}";

                                        curRect.Extent.X = (short)newWidth;
                                        curRect.Extent.Y = (short)(newHeight - refY - refY);

                                        break;
                                    }
                                case 5:
                                case 6:
                                case 7:
                                    {
                                        curCell.X = refCell.X + newCenter + (j - 5) * space2;
                                        curCell.Y = newHeight - refY;

                                        if (curCell.X < refX1)
                                            curCell.X = refX1;
                                        else if (curCell.X + curRect.Extent.X > newWidth)
                                            curCell.X = newWidth - curRect.Extent.X;

                                        break;
                                    }
                                case 8:
                                case 9:
                                case 10:
                                case 11:
                                    {
                                        int[] p = [0, 2, 3, 1];

                                        curCell.X = refCell.X + newCenter + p[j - 8] * space3;

                                        if (curCell.X < refX1)
                                            curCell.X = refX1;
                                        else if (curCell.X + curRect.Extent.X > newWidth)
                                            curCell.X = newWidth - curRect.Extent.X;

                                        break;
                                    }
                                case 12:
                                    {
                                        curCell.Y = newHeight - refY - refRect.Extent.Y;
                                        curRect.Extent.X = (short)newWidth;

                                        break;
                                    }
                                case 13:
                                    {
                                        curCell.X = curCenter - (refRect.Extent.X >> 1) + refX1;
                                        curCell.Y = newHeight - refY - refRect.Extent.Y;

                                        break;
                                    }
                            }
                            break;
                        case 1: // fleet
                            {
                                if (j <= 5)
                                {
                                    curCell.Y = newHeight - refY - (6 - j) * refRect.Extent.Y;
                                    curRect.Extent.X = (short)newWidth;
                                }
                                else
                                {
                                    curCell.X = refCell.X + newCenter + (j - 6) % 4 * space3;
                                    curCell.Y = newHeight - refY - (33 - j >> 2) * refRect.Extent.Y;

                                    if (curCell.X < refX1)
                                        curCell.X = refX1;
                                    else if (curCell.X + curRect.Extent.X > newWidth)
                                        curCell.X = newWidth - curRect.Extent.X;
                                }

                                break;
                            }
                        case 2: // form
                            {
                                curCell.X = newWidth - refX2;

                                switch (j)
                                {
                                    case 0:
                                        curRect.Extent.Y = (short)(newHeight - refY - refY);
                                        break;

                                    case 1:
                                        curCell.Y = newHeight / 9;
                                        break;
                                }

                                break;
                            }
                    }
                }
            }

            for (int i = 0; i < _races.Length; i++)
            {
                if (!q3.TryGetAsset($"{_races[i]}_tactical{oldWidth}x{oldHeight}", out asset1))
                    continue;

                tVisualGroupAsset curScene = (tVisualGroupAsset)asset1;

                curScene.Name.Value = $"{_races[i]}_tactical{newWidth}x{newHeight}";

                for (int j = 0; j < curScene.Cells.Length; j++)
                {
                    tGroupCell curCell = curScene.Cells[j];

                    tBmpAsset curBitmap = (tBmpAsset)q3.Assets[curCell.Id];
                    tSprite curSprite = GetSprite(r, curBitmap);

                    if (curBitmap.Name.Value.EndsWith("#LeftBottomPanel", StringComparison.Ordinal))
                    {
                        curCell.X = curSprite.RefX;

                        if (newHeight - curSprite.SizeY >= refHeight)
                            curCell.Y = newHeight - curSprite.RefY;
                        else
                            curCell.Y = newHeight + curSprite.SizeY;
                    }
                    else if (curSprite.SizeX >= refWidth)
                    {
                        if (curCell.Y > curSprite.SizeY)
                            curCell.Y = newHeight - curSprite.RefY;
                    }
                }
            }

            return true;
        }

        private static void ReplaceIntro(BinaryReader r, BinaryWriter w, clsQ3 q3, int oldWidth, int newWidth, int newHeight)
        {
            Contract.Assert(r != null && w != null);

            //  checks if the intro exits

            if (!q3.TryGetAsset($"Intro{oldWidth}", out tAsset asset))
                return;

            tVisualGroupAsset intro = (tVisualGroupAsset)asset;

            // checks if this is the new intro (title + fake bitmap + version text + version shadow)

            int count = intro.Cells.Length;

            if (count <= 4)
                return;

            // renames the intro

            if (oldWidth != newWidth)
                intro.Name.Value = $"Intro{newWidth}";

            // aligns the intro

            for (int i = 0; i <= count - 4; i++)
            {
                tGroupCell curCell = intro.Cells[i];
                tVisualGroupAsset curGroup = (tVisualGroupAsset)q3.Assets[curCell.Id];

                // cycles the backgrounds

                if (i == 0)
                {
                    tGroupCell[] backgrounds = curGroup.Cells;

                    int firstBackgroundId = backgrounds[0].Id;
                    int lastBackground = backgrounds.Length - 1;

                    for (int j = 0; j < lastBackground; j++)
                        backgrounds[j].Id = backgrounds[j + 1].Id;

                    backgrounds[lastBackground].Id = firstBackgroundId;
                }

                tBmpAsset curBitmap = (tBmpAsset)q3.Assets[curGroup.Cells[0].Id];
                tSprite curSprite = GetSprite(r, curBitmap);

                if (curGroup.Name.Value.Contains("Left", StringComparison.Ordinal))
                {
                    curCell.X = 320 - (newWidth >> 1);
                    curSprite.RefX = 0;
                }
                else if (curGroup.Name.Value.Contains("Right", StringComparison.Ordinal))
                {
                    curCell.X = 320 + (newWidth >> 1);
                    curSprite.RefX = (short)(curSprite.SizeX - 1);
                }
                else
                {
                    curCell.X = 320;
                    curSprite.RefX = (short)(curSprite.SizeX >> 1);
                }

                if (curGroup.Name.Value.Contains("Top", StringComparison.Ordinal))
                {
                    curCell.Y = 240 - (newHeight >> 1);
                    curSprite.RefY = 0;
                }
                else if (curGroup.Name.Value.Contains("Bottom", StringComparison.Ordinal))
                {
                    curCell.Y = 240 + (newHeight >> 1);
                    curSprite.RefY = (short)(curSprite.SizeY - 1);
                }
                else
                {
                    curCell.Y = 240;
                    curSprite.RefY = (short)(curSprite.SizeY >> 1);
                }
            }

            // ... title

            tGroupCell titleCell = intro.Cells[count - 4];
            tSprite titleSprite = GetSprite(r, (tBmpAsset)q3.Assets[((tVisualGroupAsset)q3.Assets[titleCell.Id]).Cells[0].Id]);

            // ... version

            _ = clsQ3.TryConvert(GetSprite(r, (tBmpAsset)q3.Assets[intro.Cells[count - 3].Id]), out Bitmap fakeBitmap);

            // checks if the fake bitmap is valid
            //   if not it sets the default values

            if (fakeBitmap.Width < 8 || fakeBitmap.Height < 2)
            {
                fakeBitmap = new Bitmap(10, 2, PixelFormat.Format32bppPArgb); // font size 10

                fakeBitmap.SetPixel(0, 0, Color.Blue); // bottom right of the title

                fakeBitmap.SetPixel(9, 0, Color.Goldenrod); // text color
                fakeBitmap.SetPixel(9, 1, Color.DarkGoldenrod); // shadow color
            }

            for (int i = 0; i < 2; i++)
            {
                tGroupCell curCell = intro.Cells[count - 2 + i];
                tStaticTextAsset curText = (tStaticTextAsset)q3.Assets[curCell.Id];

                // sets the alignment to the bottom left of the title

                int displacement = (fakeBitmap.Width >> 3) * i;

                curCell.X = titleCell.X - titleSprite.RefX - displacement;
                curCell.Y = titleCell.Y - titleSprite.RefY + titleSprite.SizeY - displacement;

                //  ... then adjusts its height

                Color a = fakeBitmap.GetPixel(0, fakeBitmap.Height - 1);

                if (a.R > a.G && a.R > a.B)
                    curCell.Y -= fakeBitmap.Height - 2;
                else
                    curCell.Y += fakeBitmap.Height - 2;

                // setups the textbox

                if (titleSprite.SizeX < 80)
                    curText.Extent.X = 80;
                else
                    curText.Extent.X = titleSprite.SizeX;

                curText.Extent.Y = 60;

                //  ... font style

                a = fakeBitmap.GetPixel(0, 0);

                if (a.G > a.R && a.G > a.B)
                    curText.TextInfo.FontStyle = (tTextInfo.eFontStyles)(fakeBitmap.Width | 0x2000000);
                else if (a.B > a.R && a.B > a.G)
                    curText.TextInfo.FontStyle = (tTextInfo.eFontStyles)(fakeBitmap.Width | 0x3000000);
                else
                    curText.TextInfo.FontStyle = (tTextInfo.eFontStyles)(fakeBitmap.Width | 0x1000000);

                curText.TextInfo.Index = GetColor(fakeBitmap.GetPixel(fakeBitmap.Width - 1, (fakeBitmap.Height - 1) * i));
            }
        }

        private static void CenterUIs(BinaryReader r, BinaryWriter w, clsQ3 q3, int width, int height)
        {
            Contract.Assert(r != null && w != null);

            // loading screen

            for (int i = 0; i < _languages.Length; i++)
            {
                if (q3.TryGetAsset($"{_languages[i]}ViewportScene_{width}", out tAsset asset))
                {
                    tGroupCell cell = ((tVisualGroupAsset)asset).Cells[0];

                    cell.X = width >> 1;
                    cell.Y = height >> 1;
                }
            }
        }

        private static tSprite GetSprite(BinaryReader r, tBmpAsset bitmap)
        {
            r.BaseStream.Seek(bitmap.FileOffset, SeekOrigin.Begin);

            return new(r);
        }

        private static ushort GetColor(Color c)
        {
            return (ushort)(((uint)c.R >> 3) << 10 | ((uint)c.G >> 3) << 5 | (uint)c.B >> 3);
        }
    }
}
