﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Nyerguds.Ini;

namespace RAFullMapPreviewGenerator
{
    class MapPreviewGenerator
    {
        static Dictionary<string, int> OreFrames = new Dictionary<string, int>();
        static Random MapRandom;
        const int CellSize = 24; // in pixels
        static bool IsLoaded = false;
        Palette BasePal;
        string Theater;
        string PalName;
        IniFile MapINI;
        IniFile TemplatesINI;
        static IniFile TilesetsINI;
        string TheaterFilesExtension;
        Palette Pal;
        Dictionary<String, HouseInfo> HouseColors = new Dictionary<string, HouseInfo>();
        CellStruct[,] Cells = new CellStruct[128, 128];
        List<WaypointStruct> Waypoints = new List<WaypointStruct>();
        List<UnitInfo> Units = new List<UnitInfo>();
        List<ShipInfo> Ships = new List<ShipInfo>();
        List<InfantryInfo> Infantries = new List<InfantryInfo>();
        List<SmudgeInfo> Smudges = new List<SmudgeInfo>();
        List<StructureInfo> Structures = new List<StructureInfo>();
        List<BaseStructureInfo> BaseStructures = new List<BaseStructureInfo>();
        List<CellTriggerInfo> CellsTriggers = new List<CellTriggerInfo>();
        static Dictionary<string, Palette> ColorRemaps = new Dictionary<string, Palette>();
        List<BibInfo> Bibs = new List<BibInfo>();
        static Dictionary<string, Palette> CivilianRemaps = new Dictionary<string, Palette>();
        static Dictionary<string, BuildingBibInfo> BuildingBibs = new Dictionary<string, BuildingBibInfo>();
        static Dictionary<string, int> BuildingDamageFrames = new Dictionary<string, int>();
        static Dictionary<string, string> FakeBuildings = new Dictionary<string, string>();

        static Bitmap[] SpawnLocationBitmaps = new Bitmap[8];
        int MapWidth = -1, MapHeight = -1, MapY = -1, MapX = -1;

        public MapPreviewGenerator(string FileName)
        {
            MapINI = new IniFile(FileName);

            MapHeight = MapINI.getIntValue("Map", "Height", -1);
            MapWidth = MapINI.getIntValue("Map", "Width", -1);
            MapX = MapINI.getIntValue("Map", "X", -1);
            MapY = MapINI.getIntValue("Map", "Y", -1);

            CellStruct[] Raw = new CellStruct[16384];

            Parse_Theater();
            Load_House_Colors();

            Parse_MapPack(Raw);
            Parse_OverlayPack(Raw);
            Parse_Waypoints();
            Parse_Terrain(Raw);
            Parse_Smudges();
            Parse_Units();
            Parse_Infantry();
            Parse_Ships();
            Parse_Base();
            Parse_Structures();
            Parse_Cell_Triggers();

            for (int x = 0; x < 128; x++)
            {
                for (int y = 0; y < 128; y++)
                {
                    int Index = (x * 128) + y;
                    Cells[y, x] = Raw[Index];

                    if (Is_Out_Of_Bounds(y, x))
                    {
                        Cells[y, x].IsOutOfBounds = true;
                    }
                }
            }
        }

        public Bitmap Get_Bitmap(bool OnlyDrawVisible)
        {
            Bitmap bitMap = new Bitmap(128 * CellSize, 128 * CellSize);
            Graphics g = Graphics.FromImage(bitMap);


            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    CellStruct data = Cells[x, y];

                    if (OnlyDrawVisible && data.IsOutOfBounds)
                    {
                        continue;
                    }

                    Draw_Template(data, g, x, y);

                    if (data.Overlay != null)
                    {
                        Draw_Overlay(data, g, x, y);
                    }
                }
            }

            Draw_Bibs(g);
            Draw_Base_Structures(g);
            Draw_Structures(g);

            Draw_Units(g);
            Draw_Infantries(g);
            Draw_Ships(g);

            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    CellStruct data = Cells[x, y];

                    if (OnlyDrawVisible && data.IsOutOfBounds)
                    {
                        continue;
                    }

                    if (data.Terrain != null)
                    {
                        Draw_Terrain(data, g, x, y);
                    }
                }
            }

            Draw_Smudges(g);

            Draw_Cell_Triggers(g);
            Draw_Waypoints(g);

            if (OnlyDrawVisible == false)
            {
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        CellStruct data = Cells[x,y];
                        if (data.IsOutOfBounds/* Is_Out_Of_Bounds(x, y) */)
                        {
                            Draw_Out_Of_Bounds(g, x, y);
                        }
                    }
                }
            }

            if (OnlyDrawVisible)
            {
                bitMap = Get_In_Bounds_Region(bitMap);
            }

            RenderUtils.Clear_Caches();

            return bitMap;
        }

        Bitmap Get_In_Bounds_Region(Bitmap srcBitmap)
        {
            Rectangle section = new Rectangle(MapX * TemplateReader.TileSize,
                MapY * TemplateReader.TileSize, MapWidth * TemplateReader.TileSize,
                MapHeight * TemplateReader.TileSize);



            Bitmap bmp = new Bitmap(section.Width, section.Height);
            Graphics g = Graphics.FromImage(bmp);

            // Draw the specified section of the source bitmap to the new one
            g.DrawImage(srcBitmap, 0, 0, section, GraphicsUnit.Pixel);

            return bmp;
        }

        bool Is_Out_Of_Bounds(int X, int Y)
        {
            if (MapX > X || X >= MapX + MapWidth)
                return true;

            if (MapY > Y || Y >= MapY + MapHeight)
                return true;

            return false;
        }

        void Draw_Out_Of_Bounds(Graphics g, int x, int y)
        {
            g.FillRectangle(new SolidBrush(Color.FromArgb(45, 0, 162, 232)),
                x * TemplateReader.TileSize, y * TemplateReader.TileSize,
                TemplateReader.TileSize, TemplateReader.TileSize);
        }

         void Draw_Cell_Triggers(Graphics g)
        {
            foreach (CellTriggerInfo c in CellsTriggers)
            {
                Draw_Text(g, c.Name, new Font("Thaoma", 7), Brushes.Aqua,
                    c.X * TemplateReader.TileSize, (c.Y * TemplateReader.TileSize) + 6);
            }

        }

        void Draw_Waypoints(Graphics g)
        {
            foreach (WaypointStruct wp in Waypoints)
            {
                string text = wp.Number.ToString();
                int X_Adjust = 8;
                if (text.Length == 2)
                {
                    X_Adjust = 5;
                    if (text[0] == '1')
                    {
                        X_Adjust -= 1;
                    }
                }

                Draw_Text(g, wp.Number.ToString(), new Font("Thaoma", 8), Brushes.Red,
                    (TemplateReader.TileSize * wp.X) + X_Adjust, (wp.Y * TemplateReader.TileSize) + 6);
                Draw_Rectangle(g, wp.X, wp.Y);
            }

        }

        void Draw_Text(Graphics g, string text, Font font, Brush brush, int x, int y)
        {
            RectangleF rectf = new RectangleF(x, y, 
                TemplateReader.TileSize * 2, TemplateReader.TileSize * 2);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
//            g.DrawString(text, new Font("Thaoma", 7), Brushes.GreenYellow, rectf);
            g.DrawString(text, font, brush, rectf);
        }

        void Draw_Rectangle(Graphics g, int x, int y)
        {
            Pen p = new Pen(Brushes.Red, 0.2f);
            g.DrawRectangle(p, x * TemplateReader.TileSize + 1, y * TemplateReader.TileSize + 1,
                TemplateReader.TileSize -2, TemplateReader.TileSize - 2);
        }

        void Draw_Infantries(Graphics g)
        {
            foreach (InfantryInfo i in Infantries)
            {
                Draw_Infantry(i, g);
            }

        }

        void Draw_Infantry(InfantryInfo inf, Graphics g)
        {
            Palette InfPal = null;

            if (CivilianRemaps.ContainsKey(inf.Name))
            {
                CivilianRemaps.TryGetValue(inf.Name, out InfPal);

                inf.Name = Get_Correct_Civilian_Image(inf.Name);
            }
            else
            {
                InfPal = Remap_For_House(inf.Side, ColorScheme.Secondary);
            }

            if (inf.Name == "c3")
            {
                inf.Name = "c2";
            }

            ShpReader InfShp = ShpReader.Load(General_File_String_From_Name(inf.Name));

            Bitmap TempBitmap = RenderUtils.RenderShp(InfShp, InfPal,
                Frame_From_Infantry_Angle(inf.Angle));

            int subX, subY;
            Sub_Cell_Pixel_Offsets(inf.SubCell, out subX, out subY);

            g.DrawImage(TempBitmap, inf.X * CellSize + subX, inf.Y * CellSize + subY, TempBitmap.Width, TempBitmap.Height);
        }

        string Get_Correct_Civilian_Image(string Name)
        {
            string RetName = null;
            if (Name == "c2" || Name == "c6" || Name == "c7" || Name == "c8" || Name == "c9" || Name == "c10")
            {
                RetName = "c1";
            }
            else if (Name == "c3" || Name == "c4" || Name == "c5")
            {
                RetName = "c2";
            }

            return RetName;
        }

        void Draw_Ships(Graphics g)
        {
            foreach (ShipInfo sh in Ships)
            {
                Draw_Ship(sh, g);
            }

        }

        void Draw_Ship(ShipInfo sh, Graphics g)
        {
            string Name = sh.Name;
            ShpReader ShipShp = ShpReader.Load(General_File_String_From_Name(Name));

            Palette Remap = Remap_For_House(sh.Side, ColorScheme.Secondary);

            int Frame = -1;

            if (Name != "lst" && Name != "carr")
            {
                Frame = Frame_From_Ship_Angle(sh.Angle);
            }
            else
            {
                Frame = 0;
            }

            Bitmap ShipBitmap = RenderUtils.RenderShp(ShipShp, Remap,
                Frame);

            int CenterX = (sh.X * CellSize) + 12 - (ShipBitmap.Width / 2);
            int CenterY = (sh.Y * CellSize) + 12 - (ShipBitmap.Height / 2);

            g.DrawImage(ShipBitmap, CenterX, CenterY, ShipBitmap.Width, ShipBitmap.Height);

            if (Has_Turret(Name))
            {
                Draw_Turret(g, ShipBitmap, sh.Name, sh.X, sh.Y, sh.Angle, false, Remap);

                if (Name == "ca")
                {
                    // Draw second turret for Cruiser
                    Draw_Turret(g, ShipBitmap, sh.Name, sh.X, sh.Y, sh.Angle, true, Remap);
                }
            }
        }

        private void Draw_Turret(Graphics g, Bitmap UnitBitmap, string Name, int X, int Y, int Angle, bool SecondTurret, Palette Remap)
        {
            int AdjustX = 0; int AdjustY = 0;

            int Frame = -1; 
            Bitmap TurretBitmap = Get_Turret_Bitmap(UnitBitmap, Name, Angle, Remap, out Frame);
            Get_Turret_Adjustment(Name, Frame, SecondTurret, out AdjustX, out AdjustY);

            int TurretCenterX = (X * CellSize) + 12  - (TurretBitmap.Width / 2);
            int TurretCenterY = (Y * CellSize) + 12  - (TurretBitmap.Height / 2);

            g.DrawImage(TurretBitmap, TurretCenterX + AdjustX, TurretCenterY + AdjustY, TurretBitmap.Width, TurretBitmap.Height);
        }

        void Get_Turret_Adjustment(string Name, int Frame,  bool SecondTurret, out int AdjustX, out int AdjustY)
        {
            AdjustY = 0;
            AdjustX = 0;

            switch (Name)
            {
                case "dd":
                    Get_Destroyer_Turret_Adjustments(Frame, out AdjustX, out AdjustY); break;
                case "pt":
                    Get_Gunboat_Turret_Adjustments(Frame, out AdjustX, out AdjustY); break;
                case "ca":
                    if (SecondTurret) { Get_Cruiser_Second_Turret_Adjustments(Frame, out AdjustX, out AdjustY); break; }
                    else { Get_Cruiser_First_Turret_Adjustments(Frame, out AdjustX, out AdjustY); break; }
                case "mrj":
                    Get_Mobile_Radar_Jammer_Turret_Adjustments(Frame, out AdjustX, out AdjustY);
                    Frame = 32; 
                    break;
                case "mgg":
                    Get_Mobile_Gap_Generator_Turret_Adjustments(Frame, out AdjustX, out AdjustY);
                    Frame /= 4; 
                    break;
                default: break;
            }

        }

        void Get_Mobile_Gap_Generator_Turret_Adjustments(int Frame, out int AdjustX, out int AdjustY)
        {
            AdjustX = 0; AdjustY = 0;

            switch (Frame)
            {
                case 0:
                case 1:
                case 2: 
                    AdjustX = 0; AdjustY = 3; break;
                case 3: 
                case 4: 
                case 5: 
                    AdjustX = 3; AdjustY = 1; break;
                case 6:
                case 7: 
                case 8: 
                    AdjustX = 3; AdjustY = -2; break;
                case 9:
                case 10: 
                case 11: 
                    AdjustX = 4; AdjustY = -2; break;
                case 12:
                case 13:
                case 14: 
                    AdjustX = 5; AdjustY = -6; break;
                case 15: 
                case 16: 
                case 17: 
                    AdjustX = -1; AdjustY = -6; break;
                case 18: 
                case 19: 
                case 20: 
                    AdjustX = -5; AdjustY = -5; break;
                case 21:
                case 22:
                case 23: 
                    AdjustX = -7; AdjustY = -3; break;
                case 24:
                case 25:
                case 26: 
                    AdjustX = -8; AdjustY = -1; break;
                case 27:
                case 28: 
                case 29: 
                    AdjustX = -6; AdjustY = 2; break;
                case 30:
                case 31: 
                    AdjustX = -3; AdjustY = 3; break;

                default: break;
            }
        }

        void Get_Mobile_Radar_Jammer_Turret_Adjustments(int Frame, out int AdjustX, out int AdjustY)
        {
            AdjustX = 0; AdjustY = 0;

            switch (Frame-32)
            {
                case 0: AdjustX = 0; AdjustY = -2; break;
                case 1: AdjustX = -2; AdjustY = 0; break;
                case 2: AdjustX = -3; AdjustY = 0; break;
                case 3: AdjustX = -5; AdjustY = 0; break;
                case 4: AdjustX = -6; AdjustY = 0; break;
                case 5: AdjustX = -8; AdjustY = -2; break;
                case 6: AdjustX = 0; AdjustY = -2; break;
                case 7: AdjustX = 0; AdjustY = -2; break;
                case 8: AdjustX = 0; AdjustY = -5; break;
                case 9: AdjustX = 0; AdjustY = 0; break;
                case 10: AdjustX = 0; AdjustY = 0; break;
                case 11: AdjustX = 0; AdjustY = 0; break;
                case 12: AdjustX = 0; AdjustY = 0; break;
                case 13: AdjustX = 0; AdjustY = 0; break;
                case 14: AdjustX = 0; AdjustY = 0; break;
                case 15: AdjustX = 0; AdjustY = 0; break;
                case 16: AdjustX = 0; AdjustY = 0; break;
                case 17: AdjustX = 0; AdjustY = 0; break;
                case 18: AdjustX = 0; AdjustY = 0; break;
                case 19: AdjustX = 0; AdjustY = 0; break;
                case 20: AdjustX = 0; AdjustY = 0; break;
                case 21: AdjustX = 0; AdjustY = 0; break;
                case 22: AdjustX = 0; AdjustY = 0; break;
                case 23: AdjustX = 0; AdjustY = 0; break;
                case 24: AdjustX = 0; AdjustY = 0; break;
                case 25: AdjustX = 0; AdjustY = 0; break;
                case 26: AdjustX = 0; AdjustY = 0; break;
                case 27: AdjustX = 0; AdjustY = 0; break;
                case 28: AdjustX = 0; AdjustY = 0; break;
                case 29: AdjustX = 0; AdjustY = 0; break;
                case 30: AdjustX = 0; AdjustY = 0; break;
                case 31: AdjustX = 0; AdjustY = 0; break;

                default: break;
            }
        }

        void Get_Cruiser_Second_Turret_Adjustments(int Frame, out int AdjustX, out int AdjustY)
        {
            AdjustX = 0; AdjustY = 0;

            switch (Frame)
            {
                case 0: AdjustX = 0; AdjustY = 7; break;
                case 2: AdjustX = 8; AdjustY = 6; break;
                case 4: AdjustX = 15; AdjustY = 4; break;
                case 6: AdjustX = 21; AdjustY = 1; break;
                case 8: AdjustX = 20; AdjustY = -3; break;
                case 10: AdjustX = 20; AdjustY = -8; break;
                case 12: AdjustX = 16; AdjustY = -12; break;
                case 14: AdjustX = 8; AdjustY = -13; break;
                case 16: AdjustX = 0; AdjustY = -16; break;
                case 18: AdjustX = -8; AdjustY = -13; break;
                case 20: AdjustX = -14; AdjustY = -12; break;
                case 22: AdjustX = -21; AdjustY = -8; break;
                case 24: AdjustX = -21; AdjustY = -3; break;
                case 26: AdjustX = -21; AdjustY = 2; break;
                case 28: AdjustX = -15; AdjustY = 5; break;
                case 30: AdjustX = -10; AdjustY = 8; break;

                default: break;
            }
        }

        void Get_Cruiser_First_Turret_Adjustments(int Frame, out int AdjustX, out int AdjustY)
        {
            AdjustX = 0; AdjustY = 0;

            switch (Frame)
            {
                case 0: AdjustX = 0; AdjustY = -12; break;
                case 2: AdjustX = -8; AdjustY = -12; break;
                case 4: AdjustX = -14; AdjustY = -10; break;
                case 6: AdjustX = -20; AdjustY = -8; break;
                case 8: AdjustX = -21; AdjustY = -3; break;
                case 10: AdjustX = -18; AdjustY = 1; break;
                case 12: AdjustX = -14; AdjustY = 5; break;
                case 14: AdjustX = -8; AdjustY = 8; break;
                case 16: AdjustX = 0; AdjustY = 8; break;
                case 18: AdjustX = 8; AdjustY = 8; break;
                case 20: AdjustX = 13; AdjustY = 5; break;
                case 22: AdjustX = 16; AdjustY = 2; break;
                case 24: AdjustX = 18; AdjustY = -3; break;
                case 26: AdjustX = 19; AdjustY = -7; break;
                case 28: AdjustX = 15; AdjustY = -11; break;
                case 30: AdjustX = 7; AdjustY = -12; break;

                default: break;
            }
        }

        void Get_Destroyer_Turret_Adjustments(int Frame, out int AdjustX, out int AdjustY)
        {
            AdjustX = 0; AdjustY = 0;

            switch (Frame)
            {
                case 0: AdjustX = 0; AdjustY = 0; break;
                case 2: AdjustX = 2; AdjustY = 0; break;
                case 4: AdjustX = 4; AdjustY = 0; break;
                case 6: AdjustX = 5; AdjustY = -3; break;
                case 8: AdjustX = 7; AdjustY = -3; break;
                case 10: AdjustX = 7; AdjustY = -6; break;
                case 12: AdjustX = 5; AdjustY = -8; break;
                case 14: AdjustX = 2; AdjustY = -6; break;
                case 16: AdjustX = 0; AdjustY = -6; break;
                case 18: AdjustX = 0; AdjustY = -7; break;
                case 20: AdjustX = -4; AdjustY = -8; break;
                case 22: AdjustX = -7; AdjustY = -6; break;
                case 24: AdjustX = -7; AdjustY = -4; break;
                case 26: AdjustX = -7; AdjustY = -2; break;
                case 28: AdjustX = -6; AdjustY = -1; break;
                case 30: AdjustX = -4; AdjustY = -2; break;

                default: break;
            }
        }

        void Get_Gunboat_Turret_Adjustments(int Frame, out int AdjustX, out int AdjustY)
        {
            AdjustX = 0; AdjustY = 0;

            switch (Frame)
            {
                case 0: AdjustX = -1; AdjustY = -6; break;
                case 2: AdjustX = -6; AdjustY = -5; break;
                case 4: AdjustX = -10; AdjustY = -4; break;
                case 6: AdjustX = -13; AdjustY = -2; break;
                case 8: AdjustX = -13; AdjustY = 1; break;
                case 10: AdjustX = -14; AdjustY = 4; break;
                case 12: AdjustX = -10; AdjustY = 6; break;
                case 14: AdjustX = -7; AdjustY = 9; break;
                case 16: AdjustX = -1; AdjustY = 8; break;
                case 18: AdjustX = 6; AdjustY = 8; break;
                case 20: AdjustX = 10; AdjustY = 7; break;
                case 22: AdjustX = 12; AdjustY = 4; break;
                case 24: AdjustX = 13; AdjustY = 1; break;
                case 26: AdjustX = 12; AdjustY = -2; break;
                case 28: AdjustX = 9; AdjustY = -4; break;
                case 30: AdjustX = 5; AdjustY = -6; break;

                default: break;
            }
        }

        private Bitmap Get_Turret_Bitmap(Bitmap UnitBitmap, string UnitName, int Angle, Palette Remap, out int Frame)
        {
            string TurretName = null;
            Frame = -1;

            switch (UnitName)
            {
                case "1tnk":
                case "2tnk":
                case "3tnk":
                case "4tnk":
                case "jeep":
                case "ttnk":
                case "mrj":
                    TurretName = UnitName;
                    Frame = Frame_From_Unit_Angle(Angle) + 32;
                    break;
                case "mgg":
                    TurretName = UnitName;
                    Frame = Frame_From_Unit_Angle(Angle);
                    break;

                case "stnk":
                    TurretName = UnitName;
                    Frame = Frame_From_Unit_Angle(Angle) + 38;
                    break;

                case "pt":
                    TurretName = "mgun";
                    Frame = Frame_From_Unit_Angle(Angle);
                    break;

                case "ca":
                    TurretName = "turr";
                    Frame = Frame_From_Unit_Angle(Angle);
                    break;

                case "dd":
                    TurretName = "ssam";
                    Frame = Frame_From_Unit_Angle(Angle);
                    break;
                default: break;
            }

            ShpReader TurretShp = ShpReader.Load(General_File_String_From_Name(TurretName));
            return RenderUtils.RenderShp(TurretShp, Remap, UnitName == "mgg" ? 37 : Frame);
        }

        void Draw_Units(Graphics g)
        {
            foreach (UnitInfo u in Units)
            {
                Draw_Unit(u, g);
            }

        }

        void Draw_Unit(UnitInfo u, Graphics g)
        {
            string Name = u.Name;
            ShpReader UnitShp = ShpReader.Load(General_File_String_From_Name(u.Name));

            Palette Remap = Remap_For_House(u.Side, ColorScheme.Secondary);

            int Frame = -1;

            if (Name == "ant1" || Name == "ant2" || Name == "ant3")
            {
                Frame = Frame_From_Infantry_Angle(u.Angle);
            }
            else
            {
                Frame = Frame_From_Unit_Angle(u.Angle);
            }

            Bitmap UnitBitmap = RenderUtils.RenderShp(UnitShp, Remap,
                Frame);

            int CenterX = (u.X * CellSize) + 12 - (UnitBitmap.Width / 2);
            int CenterY = (u.Y * CellSize) + 12 - (UnitBitmap.Height / 2);

            g.DrawImage(UnitBitmap, CenterX, CenterY, UnitBitmap.Width, UnitBitmap.Height);

            // Draw vehicle turret
            if (Has_Turret(Name))
            {
                Draw_Turret(g, UnitBitmap, u.Name, u.X, u.Y, u.Angle, false, Remap);
            }
        }

        bool Has_Turret(string Name)
        {
            switch (Name)
            {
                case "1tnk":
                case "2tnk":
                case "3tnk":
                case "4tnk":
                case "jeep":
                case "ttnk":
                case "stnk":
                case "mgg":
                case "mrj":
                case "pt":
                case "dd":
                case "ca":
                    return true;

                default: return false;
            }
        }

        void Draw_Base_Structures(Graphics g)
        {
            foreach (BaseStructureInfo bs in BaseStructures)
            {
                Draw_Base_Structure(bs, g);
            }
        }

        void Draw_Base_Structure(BaseStructureInfo bs, Graphics g)
        {
            string FileName = General_File_String_From_Name(bs.Name);

            if (!File.Exists(FileName))
            {
                FileName = Theater_File_String_From_Name(bs.Name);
            }

            ShpReader BaseStructShp = ShpReader.Load(FileName);

            Bitmap BaseStructBitmap = RenderUtils.RenderShp(BaseStructShp, BasePal,
                0);

            Draw_Image_With_Opacity(g, BaseStructBitmap, bs.X * CellSize, bs.Y * CellSize);

            if (bs.IsFake)
            {
                Draw_Fake_Text(g, BaseStructBitmap, bs.X, bs.Y);
            }
        }

        void Draw_Image_With_Opacity(Graphics g, Bitmap bitmap, int X, int Y)
        {
            ColorMatrix matrix = new ColorMatrix();

            //opacity 0 = completely transparent, 1 = completely opaque
            matrix.Matrix03 = 0.9f; matrix.Matrix13 = 0.01f; matrix.Matrix23 = 0.1f;
            matrix.Matrix33 = 0.01f; matrix.Matrix43 = 0.01f;

            ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Default);

            g.DrawImage(bitmap, new Rectangle(X, Y, bitmap.Width, bitmap.Height),
                0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
        }

        void Draw_Structures(Graphics g)
        {
            foreach (StructureInfo s in Structures)
            {
                Draw_Structure(s, g);
            }
        }

        void Draw_Structure(StructureInfo s, Graphics g)
        {
            string FileName = General_File_String_From_Name(s.Name);

            if (!File.Exists(FileName))
            {
                FileName = Theater_File_String_From_Name(s.Name);
            }

            ShpReader StructShp = ShpReader.Load(FileName);

            int Frame = Frame_From_Building_HP(s);
            if (s.Name == "gun" || s.Name == "agun" || s.Name == "sam") Frame += Frame_From_Unit_Angle(s.Angle);

            Bitmap StructBitmap = RenderUtils.RenderShp(StructShp,  Remap_For_House(s.Side, ColorScheme.Primary),
                Frame);

            g.DrawImage(StructBitmap, s.X * CellSize, s.Y * CellSize, StructBitmap.Width, StructBitmap.Height);

            if (s.IsFake)
            {
                Draw_Fake_Text(g, StructBitmap, s.X, s.Y);
            }
        }

        private void Draw_Fake_Text(Graphics g, Bitmap bitmap, int X, int Y)
        {
            int CenterX = (X * CellSize) + (bitmap.Width / 2);
            int CenterY = (Y * CellSize) + (bitmap.Height / 2);

            Draw_Text(g, "FAKE", new Font("Thaoma", 10), Brushes.White, CenterX - 20, CenterY - 8);
        }

        void Draw_Bibs(Graphics g)
        {
            foreach (BibInfo bib in Bibs)
            {
                Draw_Bib(g, bib.Name, bib.X, bib.Y, bib.IsBaseStructureBib);
            }
        }

        void Draw_Bib(Graphics g, string Name, int X, int Y, bool IsBaseStructureBib)
        {
            Name = Name.ToLower();
            string FileName = Theater_File_String_From_Name(Name);

            // Interior theater misses a shit ton of graphics so just return if doesn't exist
            if (Theater == "interior" && !File.Exists(FileName))
            {
                return;
            }

            ShpReader BibShp = ShpReader.Load(FileName);
            int Frame = 0;

            int maxY = -1; int maxX = -1;
            switch (Name)
            {
                case "bib1": maxY = 2; maxX = 4; break;
                case "bib2": maxY = 2; maxX = 3; break;
                case "bib3": maxY = 2; maxX = 2; break;
                default: break;
            }

            for (int y = 0; y < maxY; y++)
            {
                for (int x = 0; x < maxX; x++)
                {
                    Bitmap BibBitmap = RenderUtils.RenderShp(BibShp, Pal, Frame);
                    int bibX = (X + x) * CellSize; int bibY = (Y + y) * CellSize;

                    if (IsBaseStructureBib)
                    {
                        Draw_Image_With_Opacity(g, BibBitmap, bibX, bibY);

                    }
                    else
                    {
                        g.DrawImage(BibBitmap, bibX, bibY, BibBitmap.Width, BibBitmap.Height);
                    }

                    Frame++;
                }
            }
        }

        void Draw_Smudges(Graphics g)
        {
            foreach (SmudgeInfo sm in Smudges)
            {
                Draw_Smudge(sm, g);
            }
        }

        void Draw_Smudge(SmudgeInfo sm, Graphics g)
        {
            string Name = sm.Name.ToLower();
            if (Name == "bib1" || Name == "bib2" || Name == "bib3")
            {
                Draw_Bib(g, Name, sm.X, sm.Y, false);
            }

            ShpReader SmudgeShp = ShpReader.Load(Theater_File_String_From_Name(sm.Name));

            Bitmap StructBitmap = RenderUtils.RenderShp(SmudgeShp, Pal, sm.State);

            g.DrawImage(StructBitmap, sm.X * CellSize, sm.Y * CellSize, StructBitmap.Width, StructBitmap.Height);
        }

        void Draw_Terrain(CellStruct Cell, Graphics g, int X, int Y)
        {
            string[] TerrainData = Cell.Terrain.Split(',');

            ShpReader Shp = ShpReader.Load(Theater_File_String_From_Name(TerrainData[0]));

            int Frame = 0;

            Bitmap ShpBitmap = RenderUtils.RenderShp(Shp, Pal, Frame);
            g.DrawImage(ShpBitmap, X * CellSize, Y * CellSize, ShpBitmap.Width, ShpBitmap.Height);
        }

        void Draw_Overlay(CellStruct Cell, Graphics g, int X, int Y)
        {
            string Overlay = Cell.Overlay.ToLower();
            int Frame = 0;

            if (OreFrames.ContainsKey(Overlay.ToLower()))
            {
                Frame = -1;
                OreFrames.TryGetValue(Overlay.ToLower(), out Frame);
            }

            string FilePath = Theater_File_String_From_Name(Overlay);

            if (!File.Exists(FilePath))
            {
                FilePath = General_File_String_From_Name(Overlay);
            }

            ShpReader Shp = ShpReader.Load(FilePath);

            if (Is_Fence(Overlay))
            {
                Frame = Frame_For_Fence(Overlay, X, Y);
            }

            Bitmap ShpBitmap = RenderUtils.RenderShp(Shp, Pal, Frame);
            g.DrawImage(ShpBitmap, X * CellSize, Y * CellSize, ShpBitmap.Width, ShpBitmap.Height);
        }

        void Draw_Template(CellStruct Cell, Graphics g, int X, int Y)
        {
            string TemplateString = "CLEAR1";

            // Fix crash when parsing 6th Soviet missions, template 133 is
            // bridge2.tem and only has 10 tiles, yet the map has tile 11 for template..
            if (Cell.Template == 133 && Cell.Tile > 9)
            {
                Cell.Template = 0;
                Cell.Tile = 0;
            }

            if (Cell.Template == 0 || Cell.Template == 255 || Cell.Template == 65535)
            {
                Cell.Tile = (X % 4) + ((Y % 4) * 4);
            }
            else
            {
                TemplateString = TilesetsINI.getStringValue("TileSets", Cell.Template.ToString(), null);
            }

            TemplateReader Temp = TemplateReader.Load(Theater_File_String_From_Name(TemplateString));

            Bitmap TempBitmap = RenderUtils.RenderTemplate(Temp, Pal, Cell.Tile);
            g.DrawImage(TempBitmap, X * CellSize, Y * CellSize, CellSize, CellSize);
        }

        void Parse_Cell_Triggers()
        {
            var SectionCellTriggers = MapINI.getSectionContent("CellTriggers");

            if (SectionCellTriggers != null)
            {
                foreach (KeyValuePair<string, string> entry in SectionCellTriggers)
                {
                    int CellIndex = int.Parse(entry.Key);
                    string Name = entry.Value;

                    CellTriggerInfo c = new CellTriggerInfo();
                    c.Name = Name;
                    c.Y = CellIndex / 128;
                    c.X = CellIndex % 128;

                    CellsTriggers.Add(c);
                }
            }
        }

        void Parse_Ships()
        {
            var SectionShips = MapINI.getSectionContent("Ships");
            if (SectionShips != null)
            {
                foreach (KeyValuePair<string, string> entry in SectionShips)
                {
                    string ShipCommaString = entry.Value;
                    string[] ShipData = ShipCommaString.Split(',');

                    ShipInfo sh = new ShipInfo();
                    sh.Name = ShipData[1].ToLower();
                    sh.Side = ShipData[0];
                    sh.Angle = int.Parse(ShipData[4]);

                    int CellIndex = int.Parse(ShipData[3]);
                    sh.Y = CellIndex / 128;
                    sh.X = CellIndex % 128;

                    Ships.Add(sh);

                    //                   Console.WriteLine("Unit name = {0}, side {1}, Angle = {2}, X = {3}, Y = {4}", u.Name,
                    //                     u.Side, u.Angle, u.X, u.Y);
                }
            }
        }

        void Parse_Base()
        {
            string PlayerBase = MapINI.getStringValue("Base", "Player", null);
            if (PlayerBase != null)
            {
                BasePal = Remap_For_House(PlayerBase.ToLower(), ColorScheme.Primary);
            }

            var SectionBase = MapINI.getSectionContent("Base");
            if (SectionBase != null)
            {
                foreach (KeyValuePair<string, string> entry in SectionBase)
                {
                    // Make sure we only parse keys that are a number
                    // To prevent crashing trying to parse "Player=" and "Count="
                    int Dummy = -1;
                    if (int.TryParse(entry.Key, out Dummy) == false) continue;

                    string BaseStructureCommaString = entry.Value;

                    string[] BaseStructureData = BaseStructureCommaString.Split(',');

                    // 0=neutral,afld,256,6,0,none
                    BaseStructureInfo bs = new BaseStructureInfo();
                    bs.Name = BaseStructureData[0].ToLower();
                    int CellIndex = int.Parse(BaseStructureData[1]);
                    bs.Y = CellIndex / 128;
                    bs.X = CellIndex % 128;

                    BaseStructures.Add(bs);

                    if (bs.Name == "weap")
                    {
                        BaseStructureInfo bs2 = new BaseStructureInfo();
                        bs2.Name = "weap2";
                        bs2.X = bs.X;
                        bs2.Y = bs.Y;
                        bs2.IsFake = true;

                        BaseStructures.Add(bs2);
                    }

                    if (BuildingBibs.ContainsKey(bs.Name))
                    {
                        BuildingBibInfo bi = new BuildingBibInfo();
                        BuildingBibs.TryGetValue(bs.Name, out bi);

                        BibInfo bib = new BibInfo();
                        bib.Name = bi.Name;
                        bib.X = bs.X;
                        bib.Y = bs.Y + bi.Yoffset;
                        bib.IsBaseStructureBib = true;

                        Bibs.Add(bib);
                    }
                }
            }
        }

        void Parse_Structures()
        {
            var SectionStructures = MapINI.getSectionContent("Structures");
            if (SectionStructures != null)
            {
                foreach (KeyValuePair<string, string> entry in SectionStructures)
                {
                    string StructCommaString = entry.Value;
                    string[] StructData = StructCommaString.Split(',');

                    // 0=neutral,afld,256,6,0,none
                    StructureInfo s = new StructureInfo();
                    s.Name = StructData[1].ToLower();
                    s.Side = StructData[0];
                    s.Angle = int.Parse(StructData[4]);
                    s.HP = int.Parse(StructData[2]);
                    int CellIndex = int.Parse(StructData[3]);
                    s.Y = CellIndex / 128;
                    s.X = CellIndex % 128;

                    if (FakeBuildings.ContainsKey(s.Name))
                    {
                        string FakeName = null;
                        FakeBuildings.TryGetValue(s.Name, out FakeName);
                        s.Name = FakeName;
                        s.IsFake = true;
                    }

                    Structures.Add(s);

                    if (s.Name == "weap")
                    {
                        StructureInfo s2 = new StructureInfo();
                        s2.Name = "weap2";
                        s2.Side = s.Side;
                        s2.Angle = s.Angle;
                        s2.HP = s.HP;
                        s2.Y = s.Y;
                        s2.X = s.X;
                        s2.IsFake = s.IsFake; // HACK

                        Structures.Add(s2);
                    }

                    if (BuildingBibs.ContainsKey(s.Name))
                    {
                        BuildingBibInfo bi = new BuildingBibInfo();
                        BuildingBibs.TryGetValue(s.Name, out bi);

                        BibInfo bib = new BibInfo();
                        bib.Name = bi.Name;
                        bib.X = s.X;
                        bib.Y = s.Y + bi.Yoffset;
                        bib.IsBaseStructureBib = false;

                        Bibs.Add(bib);
                    }

                    //                   Console.WriteLine("structure name = {0}, side {1}, HP = {5}, Angle = {2}, X = {3}, Y = {4}", s.Name,
                    //                     s.Side, s.Angle, s.X, s.Y, s.HP);
                }
            }
        }

        void Parse_Infantry()
        {
            // 0=neutral,c1,256,2973,2,guard,3,none
            var SectionInfantry = MapINI.getSectionContent("Infantry");
            if (SectionInfantry != null)
            {
                foreach (KeyValuePair<string, string> entry in SectionInfantry)
                {
                    string InfCommaString = entry.Value;
                    string[] InfData = InfCommaString.Split(',');

                    InfantryInfo inf = new InfantryInfo();
                    inf.Name = InfData[1].ToLower();
                    inf.Side = InfData[0];
                    inf.Angle = int.Parse(InfData[6]);
                    inf.SubCell = int.Parse(InfData[4]);

                    int CellIndex = int.Parse(InfData[3]);
                    inf.Y = CellIndex / 128;
                    inf.X = CellIndex % 128;

                    Infantries.Add(inf);

//                    int subX; int subY;
                    //                    Sub_Cell_Pixel_Offsets(inf.SubCell, out subX, out subY);

                    //                    Console.WriteLine("infantry name = {0}, Side = {1}, Angle = {2}, SubCell = {5}, X = {3}, Y = {4}", inf.Name,
                    //                        inf.Side, inf.Angle, inf.X + subX, inf.Y + subY, inf.SubCell);
                }
            }
        }

        void Parse_Units()
        {
            var SectionUnits = MapINI.getSectionContent("Units");
            if (SectionUnits != null)
            {
                foreach (KeyValuePair<string, string> entry in SectionUnits)
                {
                    string UnitCommaString = entry.Value;
                    string[] UnitData = UnitCommaString.Split(',');

                    UnitInfo u = new UnitInfo();
                    u.Name = UnitData[1].ToLower();
                    u.Side = UnitData[0];
                    u.Angle = int.Parse(UnitData[4]);

                    int CellIndex = int.Parse(UnitData[3]);
                    u.Y = CellIndex / 128;
                    u.X = CellIndex % 128;

                    Units.Add(u);

                    //                   Console.WriteLine("Unit name = {0}, side {1}, Angle = {2}, X = {3}, Y = {4}", u.Name,
                    //                     u.Side, u.Angle, u.X, u.Y);
                }
            }
        }

        void Parse_Smudges()
        {
            var SectionSmudges = MapINI.getSectionContent("Smudge");
            if (SectionSmudges != null)
            {
                foreach (KeyValuePair<string, string> entry in SectionSmudges)
                {
                    string SmudgesCommaString = entry.Value;
                    string[] SmudgesData = SmudgesCommaString.Split(',');

                    SmudgeInfo sm = new SmudgeInfo();
                    sm.Name = SmudgesData[0];
                    int CellIndex = int.Parse(SmudgesData[1]);
                    sm.Y = CellIndex / 128;
                    sm.X = CellIndex % 128;
                    sm.State = int.Parse(SmudgesData[2]);

                    Smudges.Add(sm);
                }
            }
        }

        void Parse_Terrain(CellStruct[] Raw)
        {
            var SectionTerrrain = MapINI.getSectionContent("Terrain");

            if (SectionTerrrain != null)
            {
                foreach (KeyValuePair<string, string> entry in SectionTerrrain)
                {
                    int Cell = int.Parse(entry.Key);
                    string Terrain = entry.Value;

                    Raw[Cell].Terrain = Terrain;

                    // Console.WriteLine("{0} = {1}", Cell, Terrain);
                }
            }
        }

        void Parse_Waypoints()
        {
            Dictionary<string, string> SectionKeyValues;

            if ((SectionKeyValues = MapINI.getSectionContent("Waypoints")) != null)
            {
                foreach (KeyValuePair<string, string> entry in SectionKeyValues)
                {
                    int WayPoint = int.Parse(entry.Key);
                    int CellIndex = int.Parse(entry.Value);

                    //               Console.WriteLine("Waypoint = {0}, Index = {1}", WayPoint, CellIndex);
                    WaypointStruct WP = new WaypointStruct();
                    WP.Number = WayPoint;
                    WP.X = CellIndex % 128;
                    WP.Y = CellIndex / 128;
                    Waypoints.Add(WP);
                }
            }
        }

        void Parse_OverlayPack(CellStruct[] Raw)
        {
            MemoryStream ms2 = Get_Packed_Section("OverlayPack");
            var OverLayReader = new FastByteReader(ms2.GetBuffer());

            int i = 0;
            while (!OverLayReader.Done())
            {

                Raw[i].Overlay = Name_From_Overlay_Byte(OverLayReader.ReadByte());

                //                Console.WriteLine("{0} = {1}", i, Cells[i].Overlay);
                ++i;

                if (i == 128 * 128)
                    break;
            }
        }

        void Parse_MapPack(CellStruct[] Raw)
        {
            MemoryStream ms = Get_Packed_Section("MapPack");
            var ByteReader = new FastByteReader(ms.GetBuffer());

            int i = 0;
            while (!ByteReader.Done())
            {

                Raw[i].Template = ByteReader.ReadWord();

                //                Console.WriteLine("{0} = {1}", i, Cells[i].Template);
                ++i;

                if (i == 128 * 128)
                    break;
            }

            i = 0;
            while (!ByteReader.Done())
            {

                Raw[i].Tile = ByteReader.ReadByte();

                //                Console.WriteLine("{0} = {1}", i, Raw[i].Tile);
                ++i;


                if (i == 128 * 128)
                    break;
            }
        }

        void Parse_Theater()
        {

            Theater = MapINI.getStringValue("Map", "Theater", "temperate");
            Theater = Theater.ToLower();

            switch (Theater)
            {
                case "winter": TheaterFilesExtension = ".tem"; break;
                case "snow": TheaterFilesExtension = ".sno"; break;
                case "desert": TheaterFilesExtension = ".des"; break;
                case "interior": TheaterFilesExtension = ".int"; break;
                default: TheaterFilesExtension = ".tem"; break;
            }

            PalName = "temperat";

            switch (Theater)
            {
                case "winter": PalName = "winter"; break;
                case "snow": PalName = "snow"; break;
                case "desert": PalName = "desert"; break;
                case "interior": PalName = "interior"; break;
                default: PalName = "temperat"; break;
            }

            int[] ShadowIndex = { 3, 4 };
            Pal = Palette.Load("data/" + Theater + "/" + PalName + ".pal", ShadowIndex);
        }

        MemoryStream Get_Packed_Section(string SectionName)
        {
            var SectionKeyValues = MapINI.getSectionContent(SectionName);

            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, string> entry in SectionKeyValues)
            {
                sb.Append(entry.Value);
            }
            String Base64String = sb.ToString();

            //            Console.WriteLine(sb.ToString());

            byte[] data = Convert.FromBase64String(Base64String);
            byte[] RawBytes = new Byte[8192];

            var chunks = new List<byte[]>();
            var reader = new BinaryReader(new MemoryStream(data));

            try
            {
                while (true)
                {
                    var length = reader.ReadUInt32() & 0xdfffffff;
                    var dest = new byte[8192];
                    var src = reader.ReadBytes((int)length);

                    /*int actualLength =*/
                    Format80.DecodeInto(src, dest);

                    chunks.Add(dest);
                }
            }
            catch (EndOfStreamException) { }

            var ms = new MemoryStream();
            foreach (var chunk in chunks)
                ms.Write(chunk, 0, chunk.Length);

            ms.Position = 0;

            return ms;
        }

        string General_File_String_From_Name(string Name)
        {
            return ("data/general/" + Name + ".shp");
        }

        public Palette Remap_For_House(string HouseName, ColorScheme Scheme)
        {

            string Colour = "None";

            HouseInfo House = new HouseInfo();
            HouseColors.TryGetValue(HouseName.ToLower(), out House);

            switch (Scheme)
            {
                case ColorScheme.Primary: Colour = House.PrimaryColor; break;
                case ColorScheme.Secondary: Colour = House.SecondaryColor; break;
                default: break;
            }

//            Colour = "White";

            Palette Pal = this.Pal;
            ColorRemaps.TryGetValue(Colour, out Pal);
            return Pal;
        }

        int Frame_For_Fence(string Name, int X, int Y)
        {
            bool Top = Cell_Contains_Same_Overlay(X, Y - 1, Name);
            bool Bottom = Cell_Contains_Same_Overlay(X, Y + 1, Name);
            bool Left = Cell_Contains_Same_Overlay(X - 1, Y, Name);
            bool Right = Cell_Contains_Same_Overlay(X + 1, Y, Name);

            if (Top == true && Bottom == true && Left == true && Right == true)
            {
                return 15;
            }

            if (Top == true && Left == true && Right == true) { return 11; }
            if (Top == true && Right == true && Bottom == true) { return 7; }
            if (Top == true && Left == true && Bottom == true) { return 13; }
            if (Right == true && Left == true && Bottom == true) { return 14; }

            if (Top == true && Right == true) { return 3; }
            if (Bottom == true && Right == true) { return 6; }
            if (Bottom == true && Top == true) { return 5; }
            if (Top == true && Left == true) { return 9; }
            if (Right == true && Left == true) { return 10; }
            if (Left == true && Bottom == true) { return 12; }

            if (Top == true) { return 1; }
            if (Bottom == true) { return 4; }
            if (Left == true) { return 8; }
            if (Right == true) { return 2; }

            return 0;
        }

        bool Cell_Contains_Same_Overlay(int X, int Y, string Name)
        {
            if (Y < 0 || X < 0) return false;
            if (Y > 128 || X > 128) return false;

            if (Cells[X, Y].Overlay == null) return false;
            if (Cells[X, Y].Overlay.ToLower() == Name) return true;

            return false;
        }

        bool Is_Fence(string Name)
        {
            bool ret = false;

            switch (Name.ToLower())
            {
                case "barb":
                case "wood":
                case "sbag":
                case "cycl":
                case "brik":
                    ret = true; break;
                default: break;
            }

            return ret;
        }

        int Frame_From_Building_HP(StructureInfo s)
        {
            if (s.HP > 128) { return 0; }

            int Frame = 0;
            BuildingDamageFrames.TryGetValue(s.Name, out Frame);

            return Frame;
        }

        int Frame_From_Unit_Angle(int Angle)
        {
//            Console.WriteLine("Angle = {0}", Angle);

            if (Angle== 0) { return 0; }

            if (Angle > 248) { return 0; }
            if (Angle > 240) { return 1; }
            if (Angle > 232) { return 2; }
            if (Angle > 224) { return 3; }
            if (Angle > 216) { return 4; }
            if (Angle > 208) { return 5; }
            if (Angle > 200) { return 6; }
            if (Angle > 192) { return 7; }
            if (Angle > 184) { return 8; }
            if (Angle > 176) { return 9; }
            if (Angle > 168) { return 10; }
            if (Angle > 160) { return 11; }
            if (Angle > 152) { return 12; }
            if (Angle > 144) { return 13; }
            if (Angle > 136) { return 14; }
            if (Angle > 128) { return 15; }
            if (Angle > 120) { return 16; }
            if (Angle > 112) { return 17; }
            if (Angle > 104) { return 18; }
            if (Angle > 96) { return 19; }
            if (Angle > 88) { return 20; }
            if (Angle > 80) { return 21; }
            if (Angle > 72) { return 22; }
            if (Angle > 64) { return 23; }
            if (Angle > 56) { return 24; }
            if (Angle > 48) { return 25; }
            if (Angle > 40) { return 26; }
            if (Angle > 32) { return 27; }
            if (Angle > 24) { return 28; }
            if (Angle > 16) { return 29; }
            if (Angle > 8) { return 30; }
            if (Angle > 0) { return 31; }

            return -1;
        }

        int Frame_From_Infantry_Angle(int Angle)
        {
            //            Console.WriteLine("Angle = {0}", Angle);

            if (Angle == 0) { return 0; }

            if (Angle > 224) { return 0; }
            if (Angle > 192) { return 1; }
            if (Angle > 160) { return 2; }
            if (Angle > 128) { return 3; }
            if (Angle > 96) { return 4; }
            if (Angle > 64) { return 5; }
            if (Angle > 32) { return 6; }
            if (Angle > 0) { return 7; }

            return -1;
        }

        int Frame_From_Ship_Angle(int Angle)
        {
            //            Console.WriteLine("Angle = {0}", Angle);

            if (Angle == 0) { return 0; }

            if (Angle > 240) { return 0; }
            if (Angle > 224) { return 1; }
            if (Angle > 208) { return 2; }
            if (Angle > 192) { return 3; }
            if (Angle > 176) { return 4; }
            if (Angle > 160) { return 5; }
            if (Angle > 144) { return 6; }
            if (Angle > 128) { return 7; }
            if (Angle > 112) { return 8; }
            if (Angle > 96) { return 9; }
            if (Angle > 80) { return 10; }
            if (Angle > 64) { return 11; }
            if (Angle > 48) { return 12; }
            if (Angle > 32) { return 13; }
            if (Angle > 16) { return 14; }
            if (Angle > 0) { return 15; }

            return -1;
        }

        void Sub_Cell_Pixel_Offsets(int SubCell, out int X, out int Y)
        {
            X = -19; Y = -9;

            switch (SubCell)
            {
                case 1: X += 0; Y += 0; break;
                case 2: X += 11; Y += 0; break;
                case 3: Y += 11; break;
                case 4: X += 11; Y += 11; break;
                case 0: X += 6; Y += 6; break;
                default: break;
            }
        }

        string Name_From_Overlay_Byte(byte Byte)
        {
            switch (Byte)
            {
                case 0x0: return "SBAG";
                case 0x1: return "CYCL";
                case 0x2: return "BRIK";
                case 0x3: return "BARB";
                case 0x04: return "WOOD";
                case 0x05: return "GOLD01";
                case 0x06: return "GOLD02";
                case 0x07: return "GOLD03";
                case 0x08: return "GOLD04";

                case 0x09: return "GEM01";
                case 0x0A: return "GEM02";
                case 0x0B: return "GEM03";
                case 0x0C: return "GEM04";

                case 0x0D: return "V12";
                case 0x0E: return "V13";
                case 0x0F: return "V14";
                case 0x10: return "V15";
                case 0x11: return "V16";
                case 0x12: return "V17";
                case 0x13: return "V18";

                case 0x14: return "FPLS";
                case 0x015: return "WCRATE";
                case 0x16: return "SCRATE";
                case 0x17: return "FENC";
                case 0x18: return "WWCRATE";

                case 0xFF: return null;
                default: return null;
            }
        }

        string Theater_File_String_From_Name(string Name)
        {
            return ("data/" + Theater + "/" + Name + TheaterFilesExtension);
        }

        public static void Load()
        {
            TilesetsINI = new IniFile("data/tilesets.ini");
            MapRandom = new Random();

            Load_Civilian_Remap_Palettes();
            Load_Remap_Palettes();
            Load_Building_Damage_Frames();
            Load_Building_Bibs();
            Load_Fake_Buildings();

            Load_Ore_Frames();
        }

        private static void Load_Ore_Frames()
        {
            OreFrames.Add("gold01", 11);
            OreFrames.Add("gold02", 11);
            OreFrames.Add("gold03", 11);
            OreFrames.Add("gold04", 11);
            OreFrames.Add("gem01", 2);
            OreFrames.Add("gem02", 2);
            OreFrames.Add("gem03", 2);
        }

        static void Load_Fake_Buildings()
        {
            FakeBuildings.Add("weaf", "weap");
            FakeBuildings.Add("facf", "fact");
            FakeBuildings.Add("domf", "dome");
            FakeBuildings.Add("syrf", "syrd");
            FakeBuildings.Add("spef", "spen");
        }

        static void Load_Building_Bibs()
        {
            BuildingBibs.Add("bio", new BuildingBibInfo("bib3", 1));
            BuildingBibs.Add("atek", new BuildingBibInfo("bib3", 1));
            BuildingBibs.Add("fcom", new BuildingBibInfo("bib3", 1));
            BuildingBibs.Add("stek", new BuildingBibInfo("bib2", 1));
            BuildingBibs.Add("dome", new BuildingBibInfo("bib3", 1));
            BuildingBibs.Add("barr", new BuildingBibInfo("bib3", 1));
            BuildingBibs.Add("tent", new BuildingBibInfo("bib3", 1));
            BuildingBibs.Add("fact", new BuildingBibInfo("bib2", 2));
            BuildingBibs.Add("hosp", new BuildingBibInfo("bib3", 1));
            BuildingBibs.Add("hpad", new BuildingBibInfo("bib3", 1));
            BuildingBibs.Add("miss", new BuildingBibInfo("bib2", 1));
            BuildingBibs.Add("powr", new BuildingBibInfo("bib3", 1));
            BuildingBibs.Add("apwr", new BuildingBibInfo("bib2", 2));
            BuildingBibs.Add("proc", new BuildingBibInfo("bib2", 2));
            BuildingBibs.Add("weap", new BuildingBibInfo("bib2", 1));

        }

        static void Load_Building_Damage_Frames()
        {
            BuildingDamageFrames.Add("hbox", 2);
            BuildingDamageFrames.Add("mslo", 8);
            BuildingDamageFrames.Add("iron", 11);
            BuildingDamageFrames.Add("fcom", 1);
            BuildingDamageFrames.Add("atek", 1);
            BuildingDamageFrames.Add("pdox", 29);
            BuildingDamageFrames.Add("syrd", 1);
            BuildingDamageFrames.Add("pbox", 1);
            BuildingDamageFrames.Add("tsla", 10);
            BuildingDamageFrames.Add("agun", 64);
            BuildingDamageFrames.Add("ftur", 1);
            BuildingDamageFrames.Add("gap", 32);
            BuildingDamageFrames.Add("powr", 1);
            BuildingDamageFrames.Add("apwr", 1);
            BuildingDamageFrames.Add("stek", 1);
            BuildingDamageFrames.Add("barr", 10);
            BuildingDamageFrames.Add("tent", 10);
            BuildingDamageFrames.Add("kenn", 1);
            BuildingDamageFrames.Add("afld", 8);
            BuildingDamageFrames.Add("bio", 1);
            BuildingDamageFrames.Add("fact", 26);
            BuildingDamageFrames.Add("fix", 7);
            BuildingDamageFrames.Add("gun", 64);
            BuildingDamageFrames.Add("hosp", 4);
            BuildingDamageFrames.Add("hpad", 7);
            BuildingDamageFrames.Add("miss", 1);
            BuildingDamageFrames.Add("proc", 1);
            BuildingDamageFrames.Add("sam", 34);
            BuildingDamageFrames.Add("silo", 5);
            BuildingDamageFrames.Add("weap", 1);
            BuildingDamageFrames.Add("weap2", 4);
            BuildingDamageFrames.Add("v01", 1);
            BuildingDamageFrames.Add("v02", 1);
            BuildingDamageFrames.Add("v03", 1);
            BuildingDamageFrames.Add("v04", 2);
            BuildingDamageFrames.Add("v05", 2);
            BuildingDamageFrames.Add("v06", 1);
            BuildingDamageFrames.Add("v07", 2);
            BuildingDamageFrames.Add("v08", 1);
            BuildingDamageFrames.Add("v09", 1);
            BuildingDamageFrames.Add("v10", 1);
            BuildingDamageFrames.Add("v11", 1);
            BuildingDamageFrames.Add("v12", 1);
            BuildingDamageFrames.Add("v13", 1);
            BuildingDamageFrames.Add("v14", 1);
            BuildingDamageFrames.Add("v15", 1);
            BuildingDamageFrames.Add("v16", 1);
            BuildingDamageFrames.Add("v17", 1);
            BuildingDamageFrames.Add("v18", 1);
            BuildingDamageFrames.Add("v19", 14);
            BuildingDamageFrames.Add("v20", 3);
            BuildingDamageFrames.Add("v21", 3);
            BuildingDamageFrames.Add("v22", 3);
            BuildingDamageFrames.Add("v23", 3);
            BuildingDamageFrames.Add("v24", 1);
            BuildingDamageFrames.Add("v25", 1);
            BuildingDamageFrames.Add("v26", 1);
            BuildingDamageFrames.Add("v27", 1);
            BuildingDamageFrames.Add("v28", 1);
            BuildingDamageFrames.Add("v29", 1);
            BuildingDamageFrames.Add("v30", 1);
            BuildingDamageFrames.Add("v31", 1);
            BuildingDamageFrames.Add("v32", 1);
            BuildingDamageFrames.Add("v33", 1);
            BuildingDamageFrames.Add("v34", 1);
            BuildingDamageFrames.Add("v35", 1);
            BuildingDamageFrames.Add("v36", 1);
            BuildingDamageFrames.Add("v37", 1);
        }

        public void Load_House_Colors()
        {
            HouseColors.Add("spain", new HouseInfo("Yellow", "Yellow"));
            HouseColors.Add("greece", new HouseInfo("Blue", "Blue"));
            HouseColors.Add("ussr", new HouseInfo("Red", "Red"));
            HouseColors.Add("england", new HouseInfo("Green", "Green"));
            HouseColors.Add("ukraine", new HouseInfo("Orange", "Orange"));
            HouseColors.Add("germany", new HouseInfo("Gray", "Gray"));
            HouseColors.Add("france", new HouseInfo("Teal", "Teal"));
            HouseColors.Add("turkey", new HouseInfo("Brown", "Brown"));

            HouseColors.Add("special", new HouseInfo("Yellow", "Yellow"));
            HouseColors.Add("neutral", new HouseInfo("Yellow", "Yellow"));
            HouseColors.Add("goodguy", new HouseInfo("Blue", "Blue"));
            HouseColors.Add("badguy", new HouseInfo("Red", "Red"));

            HouseColors.Add("multi1", new HouseInfo("Yellow", "Yellow"));
            HouseColors.Add("multi2", new HouseInfo("Blue", "Blue"));
            HouseColors.Add("multi3", new HouseInfo("Red", "Red"));
            HouseColors.Add("multi4", new HouseInfo("Green", "Green"));
            HouseColors.Add("multi5", new HouseInfo("Orange", "Orange"));
            HouseColors.Add("multi6", new HouseInfo("Gray", "Gray"));
            HouseColors.Add("multi7", new HouseInfo("Teal", "Teal"));
            HouseColors.Add("multi8", new HouseInfo("Brown", "Brown"));

            foreach (string section in MapINI.getSectionNames())
            {
                string sect = section.ToLower();
                if (HouseColors.ContainsKey(sect))
                {
                    //                    Console.WriteLine("section = {0}", sect);

                    HouseInfo House = new HouseInfo();
                    HouseColors.TryGetValue(sect, out House);

                    int SecondaryColor = MapINI.getIntValue(section, "SecondaryColorScheme", -1);
                    if (SecondaryColor != -1) House.SecondaryColor = Parse_Color(SecondaryColor);

                    int PrimaryColor = MapINI.getIntValue(section, "Color", -1);
                    if (PrimaryColor != -1) House.PrimaryColor = Parse_Color(PrimaryColor);

                    PrimaryColor = MapINI.getIntValue(section, "Colour", -1);
                    if (PrimaryColor != -1) House.PrimaryColor = Parse_Color(PrimaryColor);

                    HouseColors.Remove(sect);
                    HouseColors.Add(sect, House);
                }
            }

            HouseInfo BadGuy = new HouseInfo();
            HouseColors.TryGetValue("badguy", out BadGuy);

            //            Console.WriteLine("PrimaryColor = {0}, SecondaryColor = {1}",
            //               BadGuy.PrimaryColor, BadGuy.SecondaryColor);
        }

        string Parse_Color(int Color)
        {
            switch (Color)
            {
                case 0: return "Yellow";
                case 1: return "Blue";
                case 2: return "Red";
                case 3: return "Green";
                case 4: return "Orange";
                case 5: return "Grey";
                case 6: return "Teal";
                case 7: return "Brown";
                case 8: return "White";
                case 9: return "Black";
                case 10: return "FlamingBlue";
                default: return null;
            }
        }

        static void Load_Civilian_Remap_Palettes()
        {
            int[] ShadowIndex = { 3, 4 };

            Palette C2Pal = Palette.Load("data/temperate/temperat.pal", ShadowIndex);
            C2Pal.Set_RGB(7, new RGB(255, 214, 125)); // 209
            C2Pal.Set_RGB(14, new RGB(0, 0, 0)); // 12
            C2Pal.Set_RGB(118, new RGB(28, 109, 97)); // 187
            C2Pal.Set_RGB(119, new RGB(24, 89, 76)); // 188
            C2Pal.Set_RGB(159, new RGB(255, 214, 125)); // 209
            C2Pal.Set_RGB(187, new RGB(89, 105, 149)); // 167
            C2Pal.Set_RGB(188, new RGB(85, 85, 85)); // 13
            CivilianRemaps.Add("c2", C2Pal);

            Palette C4Pal = Palette.Load("data/temperate/temperat.pal", ShadowIndex);
            C4Pal.Set_RGB(7, new RGB(28, 109, 97)); // 187
            C4Pal.Set_RGB(109, new RGB(133, 113, 101)); // 118
            C4Pal.Set_RGB(111, new RGB(113, 89, 80)); // 119
            C4Pal.Set_RGB(206, new RGB(24, 89, 76)); // 188
            C4Pal.Set_RGB(210, new RGB(36, 48, 52)); // 182
            CivilianRemaps.Add("c4", C4Pal);

            Palette C5Pal = Palette.Load("data/temperate/temperat.pal", ShadowIndex);
            C5Pal.Set_RGB(7, new RGB(149, 129, 97)); // 109
            C5Pal.Set_RGB(12, new RGB(190, 190, 190)); // 131
            C5Pal.Set_RGB(109, new RGB(117, 149, 157)); // 177
            C5Pal.Set_RGB(111, new RGB(101, 129, 137)); // 178
            C5Pal.Set_RGB(200, new RGB(125, 97, 72)); // 111
            C5Pal.Set_RGB(206, new RGB(125, 97, 72)); // 111
            C5Pal.Set_RGB(210, new RGB(36, 48, 52)); // 182
            CivilianRemaps.Add("c5", C5Pal);

            Palette C6Pal = Palette.Load("data/temperate/temperat.pal", ShadowIndex);
            C6Pal.Set_RGB(7, new RGB(93, 72, 64)); // 120
            C6Pal.Set_RGB(118, new RGB(149, 0, 0)); // 236
            C6Pal.Set_RGB(119, new RGB(113, 44, 36)); // 206
            C6Pal.Set_RGB(159, new RGB(125, 97, 72)); // 111
            CivilianRemaps.Add("c6", C6Pal);

            Palette C7Pal = Palette.Load("data/temperate/temperat.pal", ShadowIndex);
            C7Pal.Set_RGB(14, new RGB(190, 190, 190)); // 131
            C7Pal.Set_RGB(118, new RGB(255, 214, 0)); //  157
            C7Pal.Set_RGB(119, new RGB(234, 161, 64)); // 212
            C7Pal.Set_RGB(159, new RGB(170, 85, 0)); // 7
            C7Pal.Set_RGB(187, new RGB(133, 133, 101)); // 118
            C7Pal.Set_RGB(188, new RGB(113, 89, 80)); // 119
            CivilianRemaps.Add("c7", C7Pal);

            Palette C8Pal = Palette.Load("data/temperate/temperat.pal", ShadowIndex);
            C8Pal.Set_RGB(7, new RGB(36, 48, 52)); // 182
            C8Pal.Set_RGB(14, new RGB(170, 170, 170)); // 131
            C8Pal.Set_RGB(118, new RGB(198, 97, 0)); // 215
            C8Pal.Set_RGB(119, new RGB(170, 85, 0)); // 7
            C8Pal.Set_RGB(159, new RGB(36, 48, 52)); // 182
            C8Pal.Set_RGB(187, new RGB(20, 60, 157)); // 198
            C8Pal.Set_RGB(188, new RGB(16, 48, 137)); // 199
            C8Pal.Set_RGB(200, new RGB(125, 97, 72)); // 111
            CivilianRemaps.Add("c8", C8Pal);

            Palette C9Pal = Palette.Load("data/temperate/temperat.pal", ShadowIndex);
            C9Pal.Set_RGB(14, new RGB(170, 85, 0)); // 7
            C9Pal.Set_RGB(118, new RGB(161, 170, 202)); // 163
            C9Pal.Set_RGB(119, new RGB(125, 133, 174)); // 165
            C9Pal.Set_RGB(159, new RGB(210, 153, 125)); // 200
            C9Pal.Set_RGB(187, new RGB(125, 97, 72)); // 111
            C9Pal.Set_RGB(188, new RGB(85, 85, 85)); // 13
            CivilianRemaps.Add("c9", C9Pal);

            Palette C10Pal = Palette.Load("data/temperate/temperat.pal", ShadowIndex);
            C10Pal.Set_RGB(7, new RGB(113, 113, 113)); // 137
            C10Pal.Set_RGB(14, new RGB(255, 255, 255)); // 15
            C10Pal.Set_RGB(118, new RGB(230, 230, 230)); // 129
            C10Pal.Set_RGB(119, new RGB(190, 190, 190)); // 131
            C10Pal.Set_RGB(159, new RGB(113, 113, 113)); // 137
            C10Pal.Set_RGB(187, new RGB(161, 170, 202)); // 163
            C10Pal.Set_RGB(188, new RGB(125, 133, 174)); // 165
            CivilianRemaps.Add("c10", C10Pal);
        }

        static void Load_Remap_Palettes()
        {
            int[] ShadowIndex = { 3, 4 };

            ColorRemaps.Add("Yellow", Palette.Load("data/temperate/temperat.pal", ShadowIndex));

            ColorRemaps.Add("White", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(255, 255, 255), // 15
                               new RGB(225, 225, 225),  // 15
                               new RGB(238, 238, 238), // 128
                               new RGB(238, 238, 238), // 128
                               new RGB(238, 238, 238), // 128
                               new RGB(174, 174, 174), // 132
                               new RGB(174, 174, 174), // 132
                               new RGB(161, 161, 161),  // 133
                               new RGB(125, 125, 125), // 136
                               new RGB(113, 113, 113), // 137
                               new RGB(101, 101, 101), // 138
                               new RGB(101, 101, 101), // 138
                               new RGB(93, 93, 93), // 139
                               new RGB(93, 93, 93), // 139
                               new RGB(36, 36, 36), // 142
                               new RGB(20, 20, 20), // 143     
                            }));

            ColorRemaps.Add("Black", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(101, 101, 101), // 138
                               new RGB(93, 93, 93), // 139
                               new RGB(76, 76, 76), // 140
                               new RGB(76, 76, 76), // 140
                               new RGB(56, 56, 56), // 141
                               new RGB(56, 56, 56), // 141
                               new RGB(36, 36, 36), // 142
                               new RGB(36, 36, 36), // 142
                               new RGB(20, 20, 20), // 143
                               new RGB(20, 20, 20), // 143
                               new RGB(32, 28, 28), // 19
                               new RGB(20, 20, 24), // 18
                               new RGB(12, 16, 20), // 17
                               new RGB(12, 16, 20), // 17
                               new RGB(0, 0, 0), // 12
                               new RGB(0, 0, 0), // 12               
                            }));

            ColorRemaps.Add("FlamingBlue", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(226, 230, 246), // 160
                               new RGB(206, 210, 234), // 161
                               new RGB(182, 218, 255), // 192
                               new RGB(125, 190, 242), // 193
                               new RGB(68, 149, 230), // 194
                               new RGB(40, 121, 218), // 195
                               new RGB(36, 85, 206), // 196
                               new RGB(24, 68, 192), // 197
                               new RGB(20, 60, 157), // 198
                               new RGB(20, 60, 157), // 198
                               new RGB(16, 48, 137), // 199
                               new RGB(16, 48, 137), // 199
                               new RGB(24, 44, 85), // 173
                               new RGB(12, 32, 58), // 174
                               new RGB(8, 20, 52), // 175
                               new RGB(8, 20, 52), // 175     
                            }));

            ColorRemaps.Add("Blue", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(226, 230, 246), // 0
                               new RGB(206, 210, 234), // 1
                               new RGB(182, 190, 222), // 2
                               new RGB(161, 170, 202), // 3
                               new RGB(141, 149, 186), // 4
                               new RGB(125, 133, 174), // 5
                               new RGB(105, 117, 161), // 6
                               new RGB(89, 105, 149), // 7
                               new RGB(68, 85, 137), // 8
                               new RGB(56, 72, 125), // 9
                               new RGB(48, 64, 117), // 10
                               new RGB(40, 56, 109), // 11
                               new RGB(32, 44, 97), // 12
                               new RGB(24, 44, 85), // 13
                               new RGB(12, 32, 68), // 14
                               new RGB(8, 20, 52), // 15   
                            }));

            ColorRemaps.Add("Red", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(255, 93, 0), // 0
                               new RGB(255, 0, 0), // 1
                               new RGB(238, 0, 0), // 2
                               new RGB(218, 0, 0), // 3
                               new RGB(206, 0, 0), // 4
                               new RGB(190, 0, 0), // 5
                               new RGB(178, 0, 0), // 6
                               new RGB(170, 0, 0), // 7
                               new RGB(149, 0, 0), // 8
                               new RGB(133, 0, 0), // 9
                               new RGB(117, 0, 0), // 10
                               new RGB(101, 0, 0), // 11
                               new RGB(93, 8, 0), // 12
                               new RGB(76, 0, 0), // 13
                               new RGB(56, 0, 0), // 14
                               new RGB(56, 0, 0), // 15   
                            }));

            ColorRemaps.Add("Green", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(255, 230, 149), // 0
                               new RGB(255, 230, 149), // 1
                               new RGB(198, 230, 133), // 2
                               new RGB(178, 210, 125), // 3
                               new RGB(157, 190, 117), // 4
                               new RGB(137, 174, 109), // 5
                               new RGB(121, 153, 101), // 6
                               new RGB(104, 137, 93), // 7
                               new RGB(89, 117, 76), // 8
                               new RGB(76, 101, 60), // 9
                               new RGB(60, 85, 48), // 10
                               new RGB(48, 68, 36), // 11
                               new RGB(36, 52, 24), // 12
                               new RGB(36, 52, 24), // 13
                               new RGB(24, 36, 16), // 14
                               new RGB(20, 20, 20), // 15   
                            }));

            ColorRemaps.Add("Orange", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(255, 230, 149), // 0
                               new RGB(255, 214, 125), // 1
                               new RGB(246, 198, 113), // 2
                               new RGB(238, 174, 85), // 3
                               new RGB(234, 161, 64), // 4
                               new RGB(230, 145, 40), // 5
                               new RGB(214, 121, 16), // 6
                               new RGB(198, 97, 0), // 7
                               new RGB(182, 72, 0), // 8
                               new RGB(165, 56, 0), // 9
                               new RGB(153, 40, 0), // 10
                               new RGB(133, 32, 0), // 11
                               new RGB(109, 16, 0), // 12
                               new RGB(93, 8, 0), // 13
                               new RGB(76, 0, 0), // 14
                               new RGB(56, 0, 0), // 15   
                            }));

            ColorRemaps.Add("Gray", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(238, 238, 238), // 0
                               new RGB(238, 226, 218), // 1
                               new RGB(222, 206, 198), // 2
                               new RGB(206, 186, 178), // 3
                               new RGB(186, 165, 153), // 4
                               new RGB(165, 145, 133), // 5
                               new RGB(149, 125, 113), // 6
                               new RGB(133, 113, 101), // 7
                               new RGB(113, 89, 80), // 8
                               new RGB(93, 72, 64), // 9
                               new RGB(80, 60, 52), // 10
                               new RGB(80, 60, 52), // 11
                               new RGB(60, 44, 40), // 12
                               new RGB(60, 44, 40), // 13
                               new RGB(44, 28, 24), // 14
                               new RGB(44, 28, 24), // 15   
                            }));

            ColorRemaps.Add("Teal", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(93, 194, 165), // 0
                               new RGB(93, 194, 165), // 1
                               new RGB(85, 178, 153), // 2
                               new RGB(85, 178, 153), // 3
                               new RGB(76, 161, 137), // 4
                               new RGB(68, 145, 125), // 5
                               new RGB(64, 133, 117), // 6
                               new RGB(56, 117, 109), // 7
                               new RGB(28, 109, 97), // 8
                               new RGB(24, 89, 76), // 9
                               new RGB(24, 89, 76), // 10
                               new RGB(28, 64, 64), // 11
                               new RGB(12, 52, 52), // 12
                               new RGB(12, 52, 52), // 13
                               new RGB(0, 32, 32), // 14
                               new RGB(0, 32, 32), // 15   
                            }));

            ColorRemaps.Add("Brown", Palette.Load_With_Remaps("data/temperate/temperat.pal", ShadowIndex,
                new RGB[] {
                               new RGB(210, 153, 125), // 0
                               new RGB(210, 153, 125), // 1
                               new RGB(194, 137, 105), // 2
                               new RGB(182, 113, 85), // 3
                               new RGB(174, 93, 68), // 4
                               new RGB(174, 93, 68), // 5
                               new RGB(153, 76, 56), // 6
                               new RGB(133, 64, 48), // 7
                               new RGB(113, 44, 36), // 8
                               new RGB(113, 44, 36), // 9
                               new RGB(97, 36, 28), // 10
                               new RGB(93, 8, 0), // 11
                               new RGB(76, 0, 0), // 12
                               new RGB(76, 0, 0), // 13
                               new RGB(56, 0, 0), // 14
                               new RGB(56, 0, 0), // 15   
                            }));
        }
    }

    struct ShipInfo
    {
        public string Name;
        public string Side;
        public int Angle;
        public int X;
        public int Y;
    }

    struct UnitInfo
    {
        public string Name;
        public string Side;
        public int Angle;
        public int X;
        public int Y;
    }

    struct InfantryInfo
    {
        public string Name;
        public string Side;
        public int Angle;
        public int X;
        public int Y;
        public int SubCell;
    }
    struct StructureInfo
    {
        public string Name;
        public string Side;
        public int Angle;
        public int X;
        public int Y;
        public int HP;
        public bool IsFake;
    }

    struct BaseStructureInfo
    {
        public string Name;
        public int X;
        public int Y;
        public bool IsFake;
    }

    struct BibInfo
    {
        public string Name;
        public int X;
        public int Y;
        public bool IsBaseStructureBib;
    }

    struct SmudgeInfo
    {
        public string Name;
        public int X;
        public int Y;
        public int State;
    }

    struct BuildingBibInfo
    {
        public string Name;
        public int Yoffset;

        public BuildingBibInfo(string _Name, int _Yoffset)
        {
            Name = _Name;
            Yoffset = _Yoffset;
        }
    }

    struct HouseInfo
    {
        public string PrimaryColor;
        public string SecondaryColor;

        public HouseInfo(string _PrimaryColor, string _SecondaryColor)
        {
            SecondaryColor = _SecondaryColor;
            PrimaryColor = _PrimaryColor;
        }
    }

    struct CellTriggerInfo
    {
        public string Name;
        public int X;
        public int Y;
    }

    struct CellStruct
    {
        public bool IsOutOfBounds;
        public int Template;
        public int Tile;
        public string Overlay;
        public string Terrain;
    }

    struct WaypointStruct
    {
        public int Number;
        public int X;
        public int Y;
    }

    enum TerrainType
    {
        Clear = 0,
        Water,
        Road,
        Rock,
        Tree,
        River,
        Rough,
        Wall,
        Beach,
        Ore,
        Gems,
    }

    enum ColorScheme
    {
        Primary,
        Secondary
    }
}
