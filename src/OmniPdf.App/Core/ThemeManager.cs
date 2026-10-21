using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace OmniPdf.App.Core
{
    public class ThemeDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "General";
        public Color WindowBg { get; set; }
        public Color WindowFg { get; set; }
        public Color HeaderBg { get; set; }
        public Color HeaderFg { get; set; }
        public Color MenuBg { get; set; }
        public Color MenuFg { get; set; }
        public Color SubmenuBg { get; set; }
        public Color SubmenuFg { get; set; }
        public Color HoverBg { get; set; }
        public Color NavBg { get; set; }
        public Color CanvasBg { get; set; }
        public Color CardBg { get; set; }
        public Color BorderDef { get; set; }
        public Color BorderLt { get; set; }
        public Color AccentDef { get; set; }
        public Color AccentHov { get; set; }
        public Color StatusBg { get; set; }
        public Color StatusFg { get; set; }

        public static Color Hex(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromRgb(r, g, b);
            }
            if (hex.Length == 8)
            {
                byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            return Colors.Black;
        }

        public static ThemeDefinition Create(
            string id, string name, string category,
            string winBg, string winFg,
            string headerBg, string headerFg,
            string menuBg, string menuFg,
            string submenuBg, string submenuFg,
            string hoverBg, string navBg,
            string canvasBg, string cardBg,
            string borderDef, string borderLt,
            string accentDef, string accentHov,
            string statusBg, string statusFg)
        {
            return new ThemeDefinition
            {
                Id = id,
                Name = name,
                Category = category,
                WindowBg = Hex(winBg),
                WindowFg = Hex(winFg),
                HeaderBg = Hex(headerBg),
                HeaderFg = Hex(headerFg),
                MenuBg = Hex(menuBg),
                MenuFg = Hex(menuFg),
                SubmenuBg = Hex(submenuBg),
                SubmenuFg = Hex(submenuFg),
                HoverBg = Hex(hoverBg),
                NavBg = Hex(navBg),
                CanvasBg = Hex(canvasBg),
                CardBg = Hex(cardBg),
                BorderDef = Hex(borderDef),
                BorderLt = Hex(borderLt),
                AccentDef = Hex(accentDef),
                AccentHov = Hex(accentHov),
                StatusBg = Hex(statusBg),
                StatusFg = Hex(statusFg)
            };
        }
    }

    public static class ThemeManager
    {
        private static readonly List<ThemeDefinition> _themes = new();
        private static ThemeDefinition? _customTheme;

        static ThemeManager()
        {
            InitializeThemes();
            LoadCustomTheme();
        }

        public static IReadOnlyList<ThemeDefinition> AllThemes => _themes;

        public static ThemeDefinition? CustomTheme
        {
            get => _customTheme;
            set => _customTheme = value;
        }

        public static ThemeDefinition GetTheme(string nameOrId)
        {
            if (string.Equals(nameOrId, "Custom Theme", StringComparison.OrdinalIgnoreCase) && _customTheme != null)
            {
                return _customTheme;
            }

            var match = _themes.FirstOrDefault(t => 
                string.Equals(t.Name, nameOrId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Id, nameOrId, StringComparison.OrdinalIgnoreCase));

            return match ?? _themes[0]; // Defaults to Fluent Dark
        }

        public static void ApplyTheme(ThemeDefinition theme, ResourceDictionary? localResources = null)
        {
            var winBg = new SolidColorBrush(theme.WindowBg);
            var winFg = new SolidColorBrush(theme.WindowFg);
            var headerBg = new SolidColorBrush(theme.HeaderBg);
            var headerFg = new SolidColorBrush(theme.HeaderFg);
            var menuBg = new SolidColorBrush(theme.MenuBg);
            var menuFg = new SolidColorBrush(theme.MenuFg);
            var submenuBg = new SolidColorBrush(theme.SubmenuBg);
            var submenuFg = new SolidColorBrush(theme.SubmenuFg);
            var hoverBg = new SolidColorBrush(theme.HoverBg);
            var navBg = new SolidColorBrush(theme.NavBg);
            var canvasBg = new SolidColorBrush(theme.CanvasBg);
            var cardBg = new SolidColorBrush(theme.CardBg);
            var borderDef = new SolidColorBrush(theme.BorderDef);
            var borderLt = new SolidColorBrush(theme.BorderLt);
            var accentDef = new SolidColorBrush(theme.AccentDef);
            var accentHov = new SolidColorBrush(theme.AccentHov);
            var statusBg = new SolidColorBrush(theme.StatusBg);
            var statusFg = new SolidColorBrush(theme.StatusFg);

            void UpdateDict(ResourceDictionary dict)
            {
                dict["WindowBgBrush"] = winBg;
                dict["WindowFgBrush"] = winFg;
                dict["HeaderBgBrush"] = headerBg;
                dict["HeaderFgBrush"] = headerFg;
                dict["MenuBgBrush"] = menuBg;
                dict["MenuFgBrush"] = menuFg;
                dict["SubmenuBgBrush"] = submenuBg;
                dict["SubmenuFgBrush"] = submenuFg;
                dict["ItemHoverBgBrush"] = hoverBg;
                dict["NavRailBgBrush"] = navBg;
                dict["CanvasBgBrush"] = canvasBg;
                dict["CardBgBrush"] = cardBg;
                dict["BorderBrushDefault"] = borderDef;
                dict["BorderBrushLight"] = borderLt;
                dict["AccentBrushDefault"] = accentDef;
                dict["AccentHoverBrush"] = accentHov;
                dict["StatusBarBgBrush"] = statusBg;
                dict["StatusBarFgBrush"] = statusFg;
            }

            if (Application.Current != null && Application.Current.Resources != null)
            {
                UpdateDict(Application.Current.Resources);
            }

            if (localResources != null)
            {
                UpdateDict(localResources);
            }
        }

        public static void SaveCustomTheme(ThemeDefinition theme)
        {
            _customTheme = theme;
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PSD_PDF_Studio");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "custom_theme.json");

                var dto = new Dictionary<string, string>
                {
                    ["Name"] = theme.Name,
                    ["WindowBg"] = ColorToHex(theme.WindowBg),
                    ["WindowFg"] = ColorToHex(theme.WindowFg),
                    ["HeaderBg"] = ColorToHex(theme.HeaderBg),
                    ["HeaderFg"] = ColorToHex(theme.HeaderFg),
                    ["MenuBg"] = ColorToHex(theme.MenuBg),
                    ["MenuFg"] = ColorToHex(theme.MenuFg),
                    ["SubmenuBg"] = ColorToHex(theme.SubmenuBg),
                    ["SubmenuFg"] = ColorToHex(theme.SubmenuFg),
                    ["HoverBg"] = ColorToHex(theme.HoverBg),
                    ["NavBg"] = ColorToHex(theme.NavBg),
                    ["CanvasBg"] = ColorToHex(theme.CanvasBg),
                    ["CardBg"] = ColorToHex(theme.CardBg),
                    ["BorderDef"] = ColorToHex(theme.BorderDef),
                    ["BorderLt"] = ColorToHex(theme.BorderLt),
                    ["AccentDef"] = ColorToHex(theme.AccentDef),
                    ["AccentHov"] = ColorToHex(theme.AccentHov),
                    ["StatusBg"] = ColorToHex(theme.StatusBg),
                    ["StatusFg"] = ColorToHex(theme.StatusFg),
                };

                string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch { }
        }

        private static void LoadCustomTheme()
        {
            try
            {
                string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PSD_PDF_Studio", "custom_theme.json");
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    var dto = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dto != null)
                    {
                        _customTheme = ThemeDefinition.Create(
                            "custom",
                            dto.GetValueOrDefault("Name", "Custom Theme"),
                            "Custom",
                            dto.GetValueOrDefault("WindowBg", "#0F0F12"),
                            dto.GetValueOrDefault("WindowFg", "#ECECEF"),
                            dto.GetValueOrDefault("HeaderBg", "#18181B"),
                            dto.GetValueOrDefault("HeaderFg", "#F4F4F5"),
                            dto.GetValueOrDefault("MenuBg", "#18181B"),
                            dto.GetValueOrDefault("MenuFg", "#ECECEF"),
                            dto.GetValueOrDefault("SubmenuBg", "#27272A"),
                            dto.GetValueOrDefault("SubmenuFg", "#F4F4F5"),
                            dto.GetValueOrDefault("HoverBg", "#3F3F46"),
                            dto.GetValueOrDefault("NavBg", "#18181B"),
                            dto.GetValueOrDefault("CanvasBg", "#09090B"),
                            dto.GetValueOrDefault("CardBg", "#18181B"),
                            dto.GetValueOrDefault("BorderDef", "#27272A"),
                            dto.GetValueOrDefault("BorderLt", "#3F3F46"),
                            dto.GetValueOrDefault("AccentDef", "#0284C7"),
                            dto.GetValueOrDefault("AccentHov", "#0369A1"),
                            dto.GetValueOrDefault("StatusBg", "#0284C7"),
                            dto.GetValueOrDefault("StatusFg", "#FFFFFF")
                        );
                    }
                }
            }
            catch { }

            if (_customTheme == null)
            {
                _customTheme = ThemeDefinition.Create(
                    "custom", "Custom Theme", "Custom",
                    "#0F0F12", "#ECECEF",
                    "#18181B", "#F4F4F5",
                    "#18181B", "#ECECEF",
                    "#27272A", "#F4F4F5",
                    "#3F3F46", "#18181B",
                    "#09090B", "#18181B",
                    "#27272A", "#3F3F46",
                    "#0284C7", "#0369A1",
                    "#0284C7", "#FFFFFF"
                );
            }
        }

        public static string ColorToHex(Color c)
        {
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private static void InitializeThemes()
        {
            _themes.Clear();

            // =========================================================================
            // CATEGORY 1: DARK & OLED THEMES (20 THEMES)
            // =========================================================================
            Add("fluent_dark", "Fluent Dark", "Dark & OLED", "#0F0F12", "#ECECEF", "#18181B", "#F4F4F5", "#18181B", "#ECECEF", "#27272A", "#F4F4F5", "#3F3F46", "#18181B", "#09090B", "#18181B", "#27272A", "#3F3F46", "#0284C7", "#0369A1", "#0284C7", "#FFFFFF");
            Add("oled_black", "OLED Pure Black", "Dark & OLED", "#000000", "#FFFFFF", "#0A0A0A", "#FFFFFF", "#0A0A0A", "#FFFFFF", "#141414", "#FFFFFF", "#262626", "#080808", "#000000", "#0D0D0D", "#222222", "#383838", "#2563EB", "#1D4ED8", "#1E40AF", "#FFFFFF");
            Add("midnight_navy", "Midnight Navy", "Dark & OLED", "#0A1128", "#E2E8F0", "#101F42", "#F8FAFC", "#101F42", "#E2E8F0", "#1C2D5A", "#F8FAFC", "#283E75", "#0E1838", "#060B1C", "#14244E", "#23386D", "#344F94", "#38BDF8", "#0284C7", "#0284C7", "#FFFFFF");
            Add("charcoal_slate", "Charcoal Slate", "Dark & OLED", "#1E222A", "#D1D5DB", "#282C34", "#F3F4F6", "#282C34", "#D1D5DB", "#32363E", "#F9FAFB", "#3E4451", "#21252B", "#181A1F", "#282C34", "#3E4451", "#4B5263", "#61AFEF", "#4D8ECC", "#4D8ECC", "#FFFFFF");
            Add("obsidian_void", "Obsidian Void", "Dark & OLED", "#0B0C10", "#C5C6C7", "#1F2833", "#66FCF1", "#1F2833", "#C5C6C7", "#273240", "#66FCF1", "#334152", "#161D26", "#050608", "#1A222C", "#2E3C4D", "#45A29E", "#66FCF1", "#45A29E", "#45A29E", "#0B0C10");
            Add("eclipse_dark", "Eclipse Dark", "Dark & OLED", "#121214", "#E4E4E7", "#1A1A1E", "#FAFAFA", "#1A1A1E", "#E4E4E7", "#26262C", "#FAFAFA", "#36363E", "#16161A", "#0C0C0E", "#1E1E24", "#2E2E38", "#42424E", "#8B5CF6", "#7C3AED", "#7C3AED", "#FFFFFF");
            Add("deep_space", "Deep Space", "Dark & OLED", "#050510", "#E0E7FF", "#0C0E24", "#EEF2FF", "#0C0E24", "#E0E7FF", "#171B3E", "#EEF2FF", "#252B5C", "#080A1C", "#03030A", "#111433", "#222756", "#353D82", "#6366F1", "#4F46E5", "#4338CA", "#FFFFFF");
            Add("carbon_fiber", "Carbon Fiber", "Dark & OLED", "#141416", "#D4D4D8", "#1E1E22", "#F4F4F5", "#1E1E22", "#D4D4D8", "#2A2A30", "#F4F4F5", "#3A3A42", "#18181C", "#0F0F10", "#222228", "#33333C", "#484854", "#F59E0B", "#D97706", "#D97706", "#FFFFFF");
            Add("dark_titanium", "Dark Titanium", "Dark & OLED", "#1A1D20", "#E5E7EB", "#22262B", "#F9FAFB", "#22262B", "#E5E7EB", "#2D3339", "#F9FAFB", "#3B424A", "#1E2226", "#121416", "#262B30", "#373E47", "#4D5663", "#14B8A6", "#0D9488", "#0F766E", "#FFFFFF");
            Add("raven_black", "Raven Black", "Dark & OLED", "#0D0E11", "#E1E2E6", "#15171C", "#F0F1F5", "#15171C", "#E1E2E6", "#1F2229", "#F0F1F5", "#2D313B", "#111317", "#08090B", "#191B21", "#282C35", "#3D424F", "#06B6D4", "#0891B2", "#0891B2", "#FFFFFF");
            Add("abyss_blue", "Abyss Blue", "Dark & OLED", "#040D1A", "#DBEAFE", "#0A192F", "#EFF6FF", "#0A192F", "#DBEAFE", "#122B4F", "#EFF6FF", "#1D3E6F", "#071324", "#02070E", "#0D2240", "#193B6B", "#275494", "#3B82F6", "#2563EB", "#1D4ED8", "#FFFFFF");
            Add("void_black", "Void Black", "Dark & OLED", "#050505", "#F5F5F5", "#0F0F0F", "#FFFFFF", "#0F0F0F", "#F5F5F5", "#1C1C1C", "#FFFFFF", "#2E2E2E", "#0A0A0A", "#000000", "#141414", "#262626", "#404040", "#EC4899", "#DB2777", "#BE185D", "#FFFFFF");
            Add("stealth_dark", "Stealth Dark", "Dark & OLED", "#121314", "#CED0D4", "#1B1D1F", "#F1F2F4", "#1B1D1F", "#CED0D4", "#26292C", "#F1F2F4", "#363A3E", "#16181A", "#0C0D0E", "#202326", "#313539", "#464C52", "#84CC16", "#65A30D", "#4D7C0F", "#FFFFFF");
            Add("nightfall_indigo", "Nightfall Indigo", "Dark & OLED", "#0F0C20", "#E0E7FF", "#181432", "#EEF2FF", "#181432", "#E0E7FF", "#241F48", "#EEF2FF", "#342E64", "#130F2A", "#090714", "#1D183D", "#2F285C", "#453C82", "#A855F7", "#9333EA", "#7E22CE", "#FFFFFF");
            Add("gothic_charcoal", "Gothic Charcoal", "Dark & OLED", "#111113", "#D8D8DC", "#1A1A1E", "#F4F4F6", "#1A1A1E", "#D8D8DC", "#27272D", "#F4F4F6", "#37373F", "#151518", "#0A0A0C", "#202025", "#32323A", "#474753", "#EF4444", "#DC2626", "#B91C1C", "#FFFFFF");
            Add("gunmetal_steel", "Gunmetal Steel", "Dark & OLED", "#181B20", "#DEE2E6", "#21252B", "#F8F9FA", "#21252B", "#DEE2E6", "#2B313A", "#F8F9FA", "#3B424D", "#1C2026", "#111417", "#262A32", "#373E4A", "#4B5565", "#0EA5E9", "#0284C7", "#0369A1", "#FFFFFF");
            Add("cosmic_void", "Cosmic Void", "Dark & OLED", "#0A0914", "#E9D5FF", "#151326", "#FAF5FF", "#151326", "#E9D5FF", "#211E3B", "#FAF5FF", "#322E56", "#0E0D1C", "#05040B", "#1B1832", "#2B274F", "#403A72", "#C084FC", "#A855F7", "#9333EA", "#FFFFFF");
            Add("graphite_pro", "Graphite Pro", "Dark & OLED", "#161719", "#DADCDE", "#202124", "#F1F3F4", "#202124", "#DADCDE", "#2D2F33", "#F1F3F4", "#3D4045", "#1B1C1E", "#0E0F10", "#26272A", "#37393D", "#4E5157", "#10B981", "#059669", "#047857", "#FFFFFF");
            Add("black_diamond", "Black Diamond", "Dark & OLED", "#0B0C0E", "#E2E4E8", "#131518", "#F4F5F7", "#131518", "#E2E4E8", "#1E2025", "#F4F5F7", "#2B2E35", "#0F1013", "#070708", "#181A1E", "#26282E", "#3A3D46", "#38BDF8", "#0284C7", "#0284C7", "#FFFFFF");
            Add("dark_nebula", "Dark Nebula", "Dark & OLED", "#0B061A", "#F3E8FF", "#150D30", "#FAF5FF", "#150D30", "#F3E8FF", "#24174F", "#FAF5FF", "#362473", "#100A25", "#05020D", "#1C1140", "#2F1E68", "#48309B", "#F43F5E", "#E11D48", "#BE123C", "#FFFFFF");

            // =========================================================================
            // CATEGORY 2: LIGHT, PAPER & MINIMALIST (20 THEMES)
            // =========================================================================
            Add("modern_light", "Modern Light", "Light & Minimalist", "#F8FAFC", "#0F172A", "#F1F5F9", "#0F172A", "#F1F5F9", "#0F172A", "#FFFFFF", "#0F172A", "#E2E8F0", "#F1F5F9", "#E2E8F0", "#FFFFFF", "#CBD5E1", "#94A3B8", "#0284C7", "#0369A1", "#0284C7", "#FFFFFF");
            Add("paper_white", "Classic Paper White", "Light & Minimalist", "#FAFAFA", "#171717", "#F5F5F5", "#171717", "#F5F5F5", "#171717", "#FFFFFF", "#171717", "#E5E5E5", "#F5F5F5", "#EAEAEA", "#FFFFFF", "#D4D4D4", "#A3A3A3", "#2563EB", "#1D4ED8", "#2563EB", "#FFFFFF");
            Add("fluent_pure_white", "Fluent Pure White", "Light & Minimalist", "#FFFFFF", "#1E293B", "#F8FAFC", "#0F172A", "#F8FAFC", "#1E293B", "#FFFFFF", "#0F172A", "#F1F5F9", "#F8FAFC", "#F1F5F9", "#FFFFFF", "#E2E8F0", "#CBD5E1", "#0284C7", "#0369A1", "#0284C7", "#FFFFFF");
            Add("clean_snow", "Clean Snow", "Light & Minimalist", "#F9FBFC", "#1A202C", "#EDF2F7", "#1A202C", "#EDF2F7", "#1A202C", "#FFFFFF", "#1A202C", "#E2E8F0", "#EDF2F7", "#E2E8F0", "#FFFFFF", "#CBD5E0", "#A0AEC0", "#3182CE", "#2B6CB0", "#3182CE", "#FFFFFF");
            Add("minimal_ivory", "Minimal Ivory", "Light & Minimalist", "#FDFAF5", "#2D2A26", "#F6F1E9", "#2D2A26", "#F6F1E9", "#2D2A26", "#FFFFFF", "#2D2A26", "#EBE4D8", "#F6F1E9", "#EBE4D8", "#FFFFFF", "#DDD5C7", "#C5BBAA", "#B45309", "#92400E", "#B45309", "#FFFFFF");
            Add("cloud_white", "Cloud White", "Light & Minimalist", "#F4F6F8", "#212B36", "#E9ECEF", "#212B36", "#E9ECEF", "#212B36", "#FFFFFF", "#212B36", "#DFE3E8", "#E9ECEF", "#DFE3E8", "#FFFFFF", "#C4CDD5", "#919EAB", "#00A76F", "#007867", "#00A76F", "#FFFFFF");
            Add("pearl_studio", "Pearl Studio", "Light & Minimalist", "#F6F7FB", "#1C1E23", "#ECEEF5", "#1C1E23", "#ECEEF5", "#1C1E23", "#FFFFFF", "#1C1E23", "#DFE2EE", "#ECEEF5", "#DFE2EE", "#FFFFFF", "#CCD1E2", "#A6ADC2", "#6366F1", "#4F46E5", "#6366F1", "#FFFFFF");
            Add("platinum_pro", "Platinum Pro", "Light & Minimalist", "#F2F4F7", "#101828", "#E4E7EC", "#101828", "#E4E7EC", "#101828", "#FFFFFF", "#101828", "#D0D5DD", "#E4E7EC", "#D0D5DD", "#FFFFFF", "#98A2B3", "#667085", "#3538CD", "#2D31A6", "#3538CD", "#FFFFFF");
            Add("crisp_light", "Crisp Light", "Light & Minimalist", "#F8FAFC", "#020617", "#F1F5F9", "#020617", "#F1F5F9", "#020617", "#FFFFFF", "#020617", "#E2E8F0", "#F1F5F9", "#E2E8F0", "#FFFFFF", "#CBD5E1", "#94A3B8", "#0284C7", "#0369A1", "#0284C7", "#FFFFFF");
            Add("soft_alabaster", "Soft Alabaster", "Light & Minimalist", "#FAF9F6", "#292524", "#F5F5F0", "#292524", "#F5F5F0", "#292524", "#FFFFFF", "#292524", "#EAEAE2", "#F5F5F0", "#EAEAE2", "#FFFFFF", "#D8D8CE", "#BDBDB0", "#78716C", "#57534E", "#78716C", "#FFFFFF");
            Add("warm_parchment", "Warm Parchment", "Light & Minimalist", "#FDFBF7", "#3E2723", "#F7F2E8", "#3E2723", "#F7F2E8", "#3E2723", "#FFFFFF", "#3E2723", "#EBE2D2", "#F7F2E8", "#EBE2D2", "#FFFFFF", "#DDD1BD", "#BEAE95", "#8D6E63", "#6D4C41", "#8D6E63", "#FFFFFF");
            Add("linen_cream", "Linen Cream", "Light & Minimalist", "#FAF7F2", "#382E2B", "#F2ECE3", "#382E2B", "#F2ECE3", "#382E2B", "#FFFFFF", "#382E2B", "#E4DACD", "#F2ECE3", "#E4DACD", "#FFFFFF", "#D3C6B6", "#B3A28F", "#A16207", "#854D0E", "#A16207", "#FFFFFF");
            Add("bone_white", "Bone White", "Light & Minimalist", "#F9F8F6", "#27272A", "#F0EDE8", "#27272A", "#F0EDE8", "#27272A", "#FFFFFF", "#27272A", "#E3DFD7", "#F0EDE8", "#E3DFD7", "#FFFFFF", "#D1CBC1", "#B3ABA0", "#52525B", "#3F3F46", "#52525B", "#FFFFFF");
            Add("cotton_light", "Cotton Light", "Light & Minimalist", "#FBFCFE", "#1E232A", "#F3F5FA", "#1E232A", "#F3F5FA", "#1E232A", "#FFFFFF", "#1E232A", "#E5E9F2", "#F3F5FA", "#E5E9F2", "#FFFFFF", "#D1D8E6", "#ACB7CC", "#4F46E5", "#4338CA", "#4F46E5", "#FFFFFF");
            Add("silk_white", "Silk White", "Light & Minimalist", "#FAF9FD", "#241F31", "#F2F0F9", "#241F31", "#F2F0F9", "#241F31", "#FFFFFF", "#241F31", "#E4E0F3", "#F2F0F9", "#E4E0F3", "#FFFFFF", "#CFCAE8", "#AAA3D0", "#7C3AED", "#6D28D9", "#7C3AED", "#FFFFFF");
            Add("polar_frost", "Polar Frost", "Light & Minimalist", "#F4F9FD", "#102A43", "#E6F0FA", "#102A43", "#E6F0FA", "#102A43", "#FFFFFF", "#102A43", "#D2E3F5", "#E6F0FA", "#D2E3F5", "#FFFFFF", "#BACDE3", "#8FA9C7", "#0284C7", "#0369A1", "#0284C7", "#FFFFFF");
            Add("milk_glass", "Milk Glass", "Light & Minimalist", "#F7F8F9", "#1F2328", "#EBEEF1", "#1F2328", "#EBEEF1", "#1F2328", "#FFFFFF", "#1F2328", "#DEE2E6", "#EBEEF1", "#DEE2E6", "#FFFFFF", "#CCD1D8", "#A7AFBA", "#0969DA", "#0854C0", "#0969DA", "#FFFFFF");
            Add("crisp_silver", "Crisp Silver", "Light & Minimalist", "#F3F4F6", "#111827", "#E5E7EB", "#111827", "#E5E7EB", "#111827", "#FFFFFF", "#111827", "#D1D5DB", "#E5E7EB", "#D1D5DB", "#FFFFFF", "#9CA3AF", "#6B7280", "#4B5563", "#374151", "#4B5563", "#FFFFFF");
            Add("opal_glaze", "Opal Glaze", "Light & Minimalist", "#F8FBFA", "#13343B", "#EDF5F4", "#13343B", "#EDF5F4", "#13343B", "#FFFFFF", "#13343B", "#DEECE9", "#EDF5F4", "#DEECE9", "#FFFFFF", "#C6DDD8", "#98BEB5", "#0D9488", "#0F766E", "#0D9488", "#FFFFFF");
            Add("nordic_light", "Nordic Light", "Light & Minimalist", "#F4F7FB", "#2E3440", "#E5E9F0", "#2E3440", "#E5E9F0", "#2E3440", "#ECEFF4", "#2E3440", "#D8DEE9", "#E5E9F0", "#D8DEE9", "#FFFFFF", "#C2CBD9", "#9BA8BD", "#5E81AC", "#81A1C1", "#5E81AC", "#ECEFF4");

            // =========================================================================
            // CATEGORY 3: READING, SEPIA, WARM & NATURE (18 THEMES)
            // =========================================================================
            Add("sepia_reading", "Sepia Reading", "Sepia & Nature", "#F5EBE1", "#453221", "#EADBC4", "#453221", "#EADBC4", "#453221", "#FDF8EE", "#453221", "#DFCEB4", "#EADBC4", "#DFCEB4", "#FDF8EE", "#D7C4A5", "#C3B091", "#8D6E63", "#795548", "#8D6E63", "#FFFFFF");
            Add("amber_comfort", "Amber Comfort", "Sepia & Nature", "#FAF0E6", "#422800", "#F2E0C9", "#422800", "#F2E0C9", "#422800", "#FFF9F2", "#422800", "#E5CEA9", "#F2E0C9", "#E5CEA9", "#FFF9F2", "#D6BC91", "#BFA06F", "#D97706", "#B45309", "#D97706", "#FFFFFF");
            Add("warm_desert", "Warm Desert", "Sepia & Nature", "#EFE6D8", "#4A3B32", "#E3D5C1", "#4A3B32", "#E3D5C1", "#4A3B32", "#F9F3EA", "#4A3B32", "#D2C1A8", "#E3D5C1", "#D2C1A8", "#F9F3EA", "#C4B094", "#AB9576", "#B45309", "#92400E", "#92400E", "#FFFFFF");
            Add("forest_emerald", "Forest Emerald", "Sepia & Nature", "#061A14", "#34D399", "#0A2920", "#34D399", "#0A2920", "#34D399", "#0D3529", "#34D399", "#047857", "#0A2920", "#04120E", "#0D3529", "#059669", "#10B981", "#10B981", "#059669", "#059669", "#FFFFFF");
            Add("pine_needle", "Pine Needle", "Sepia & Nature", "#081C15", "#A7F3D0", "#0D2B21", "#A7F3D0", "#0D2B21", "#A7F3D0", "#143C2E", "#A7F3D0", "#1B4D3E", "#0D2B21", "#05130E", "#143C2E", "#2D6A4F", "#40916C", "#52B788", "#40916C", "#2D6A4F", "#FFFFFF");
            Add("sage_green", "Sage Green", "Sepia & Nature", "#EDF3EF", "#2D4034", "#E0EAE3", "#2D4034", "#E0EAE3", "#2D4034", "#F7FAF8", "#2D4034", "#CCDCCF", "#E0EAE3", "#CCDCCF", "#F7FAF8", "#B5CBBB", "#96B19E", "#4B7C59", "#3A6345", "#4B7C59", "#FFFFFF");
            Add("deep_moss", "Deep Moss", "Sepia & Nature", "#101912", "#C6DEC6", "#19261C", "#E0EEE0", "#19261C", "#C6DEC6", "#233527", "#E0EEE0", "#324937", "#19261C", "#0B110C", "#233527", "#435F48", "#5A7C60", "#6A994E", "#52793C", "#52793C", "#FFFFFF");
            Add("olive_meadow", "Olive Meadow", "Sepia & Nature", "#F3F5EE", "#384126", "#E5E9DC", "#384126", "#E5E9DC", "#384126", "#FAFBF7", "#384126", "#D4DBC6", "#E5E9DC", "#D4DBC6", "#FAFBF7", "#BFCAA9", "#A4B289", "#65A30D", "#4D7C0F", "#65A30D", "#FFFFFF");
            Add("bamboo_grove", "Bamboo Grove", "Sepia & Nature", "#0C1F17", "#D1FAE5", "#133024", "#ECFDF5", "#133024", "#D1FAE5", "#1B4232", "#ECFDF5", "#265944", "#133024", "#081510", "#1B4232", "#347358", "#459371", "#10B981", "#059669", "#059669", "#FFFFFF");
            Add("matcha_tea", "Matcha Tea", "Sepia & Nature", "#F1F5EB", "#334024", "#E1EACF", "#334024", "#E1EACF", "#334024", "#FAFBF7", "#334024", "#CCDAB1", "#E1EACF", "#CCDAB1", "#FAFBF7", "#B5C793", "#98AC71", "#84CC16", "#65A30D", "#65A30D", "#FFFFFF");
            Add("terracotta_clay", "Terracotta Clay", "Sepia & Nature", "#FAF2ED", "#4A2511", "#F2E2D6", "#4A2511", "#F2E2D6", "#4A2511", "#FFF9F5", "#4A2511", "#E4C8B4", "#F2E2D6", "#E4C8B4", "#FFF9F5", "#D3A98D", "#BF8865", "#EA580C", "#C2410C", "#C2410C", "#FFFFFF");
            Add("sunset_tangerine", "Sunset Tangerine", "Sepia & Nature", "#1A0E08", "#FED7AA", "#2A180E", "#FFEDD5", "#2A180E", "#FED7AA", "#3E2315", "#FFEDD5", "#56301D", "#2A180E", "#110905", "#3E2315", "#763F24", "#9E542F", "#F97316", "#EA580C", "#EA580C", "#FFFFFF");
            Add("peach_blossom", "Peach Blossom", "Sepia & Nature", "#FEF6F3", "#4C2B22", "#FCEAE3", "#4C2B22", "#FCEAE3", "#4C2B22", "#FFFFFF", "#4C2B22", "#F7D2C4", "#FCEAE3", "#F7D2C4", "#FFFFFF", "#F0B5A2", "#E4917B", "#FB923C", "#F97316", "#FB923C", "#FFFFFF");
            Add("autumn_rust", "Autumn Rust", "Sepia & Nature", "#1F120E", "#FDBA74", "#2E1B15", "#FED7AA", "#2E1B15", "#FDBA74", "#42281F", "#FED7AA", "#5B362A", "#2E1B15", "#140B09", "#42281F", "#784637", "#9E5C49", "#C2410C", "#9A3412", "#9A3412", "#FFFFFF");
            Add("sandstone_warm", "Sandstone Warm", "Sepia & Nature", "#F7F3EE", "#42372F", "#ECE5DC", "#42372F", "#ECE5DC", "#42372F", "#FCFAF7", "#42372F", "#DFD4C5", "#ECE5DC", "#DFD4C5", "#FCFAF7", "#CBC0AE", "#B1A18C", "#A8A29E", "#78716C", "#78716C", "#FFFFFF");
            Add("cedar_wood", "Cedar Wood", "Sepia & Nature", "#1A1412", "#E7D8D3", "#28201D", "#F5EBE6", "#28201D", "#E7D8D3", "#3B302C", "#F5EBE6", "#50423D", "#28201D", "#110D0B", "#3B302C", "#6B5851", "#8C746B", "#B45309", "#92400E", "#92400E", "#FFFFFF");
            Add("coffee_roast", "Coffee Roast", "Sepia & Nature", "#14110E", "#E2D9D2", "#211C18", "#F2ECE6", "#211C18", "#E2D9D2", "#312B25", "#F2ECE6", "#443C34", "#211C18", "#0D0A08", "#312B25", "#5D5247", "#7E7062", "#78350F", "#451A03", "#78350F", "#FFFFFF");
            Add("earth_ochre", "Earth Ochre", "Sepia & Nature", "#1A170F", "#FDE68A", "#282318", "#FEF3C7", "#282318", "#FDE68A", "#3B3423", "#FEF3C7", "#524831", "#282318", "#100E09", "#3B3423", "#6E6142", "#938158", "#CA8A04", "#A16207", "#A16207", "#FFFFFF");

            // =========================================================================
            // CATEGORY 4: DEVELOPER, IDE & CODING PALETTES (18 THEMES)
            // =========================================================================
            Add("dracula_pro", "Dracula Pro", "Developer & IDE", "#21222C", "#F8F8F2", "#282A36", "#F8F8F2", "#282A36", "#F8F8F2", "#343746", "#F8F8F2", "#44475A", "#21222C", "#191A21", "#282A36", "#44475A", "#6272A4", "#BD93F9", "#FF79C6", "#BD93F9", "#282A36");
            Add("nord_frost", "Nord Frost", "Developer & IDE", "#242933", "#ECEFF4", "#2E3440", "#ECEFF4", "#2E3440", "#ECEFF4", "#3B4252", "#ECEFF4", "#434C5E", "#242933", "#1D212A", "#2E3440", "#4C566A", "#5E81AC", "#88C0D0", "#81A1C1", "#5E81AC", "#ECEFF4");
            Add("monokai_pro", "Monokai Pro", "Developer & IDE", "#221F22", "#F7F1FF", "#2D2A2E", "#F7F1FF", "#2D2A2E", "#F7F1FF", "#3A363B", "#F7F1FF", "#4A454C", "#221F22", "#19181A", "#2D2A2E", "#544F57", "#727072", "#FFD866", "#FC9867", "#FF6188", "#221F22");
            Add("one_dark_pro", "One Dark Pro", "Developer & IDE", "#21252B", "#ABB2BF", "#282C34", "#D7DAE0", "#282C34", "#ABB2BF", "#31363F", "#D7DAE0", "#3E4451", "#21252B", "#181A1F", "#282C34", "#4B5263", "#5C6370", "#61AFEF", "#4D8ECC", "#4D8ECC", "#FFFFFF");
            Add("solarized_dark", "Solarized Dark", "Developer & IDE", "#00212B", "#93A1A1", "#073642", "#EEE8D5", "#073642", "#93A1A1", "#0A4656", "#EEE8D5", "#0E586C", "#00212B", "#00141A", "#073642", "#586E75", "#657B83", "#268BD2", "#2AA198", "#2AA198", "#002B36");
            Add("solarized_light", "Solarized Light", "Developer & IDE", "#FDF6E3", "#586E75", "#EEE8D5", "#073642", "#EEE8D5", "#586E75", "#FFFFFF", "#073642", "#E0D7C0", "#EEE8D5", "#E0D7C0", "#FFFFFF", "#93A1A1", "#839496", "#268BD2", "#2AA198", "#268BD2", "#FFFFFF");
            Add("github_dark", "GitHub Dark", "Developer & IDE", "#0D1117", "#C9D1D9", "#161B22", "#F0F6FC", "#161B22", "#C9D1D9", "#21262D", "#F0F6FC", "#30363D", "#0D1117", "#010409", "#161B22", "#30363D", "#484F58", "#58A6FF", "#388BFD", "#1F6FEB", "#FFFFFF");
            Add("github_light", "GitHub Light", "Developer & IDE", "#FFFFFF", "#24292F", "#F6F8FA", "#24292F", "#F6F8FA", "#24292F", "#FFFFFF", "#24292F", "#EAEEF2", "#F6F8FA", "#AFB8C1", "#FFFFFF", "#D0D7DE", "#AFB8C1", "#0969DA", "#0550AE", "#0969DA", "#FFFFFF");
            Add("tokyo_night", "Tokyo Night", "Developer & IDE", "#16161E", "#A9B1D6", "#1F2335", "#C0CAF5", "#1F2335", "#A9B1D6", "#292E42", "#C0CAF5", "#383E5A", "#16161E", "#101014", "#1F2335", "#414868", "#565F89", "#7AA2F7", "#2AC3DE", "#7AA2F7", "#1A1B26");
            Add("catppuccin_mocha", "Catppuccin Mocha", "Developer & IDE", "#181825", "#CDD6F4", "#1E1E2E", "#CDD6F4", "#1E1E2E", "#CDD6F4", "#26263A", "#CDD6F4", "#313244", "#181825", "#11111B", "#1E1E2E", "#45475A", "#585B70", "#89B4FA", "#CBA6F7", "#89B4FA", "#11111B");
            Add("catppuccin_latte", "Catppuccin Latte", "Developer & IDE", "#EFF1F5", "#4C4F69", "#E6E9EF", "#4C4F69", "#E6E9EF", "#4C4F69", "#FFFFFF", "#4C4F69", "#DCE0E8", "#E6E9EF", "#DCE0E8", "#FFFFFF", "#CCD0DA", "#BCC0CC", "#1E66F5", "#7287FD", "#1E66F5", "#FFFFFF");
            Add("gruvbox_dark", "Gruvbox Dark", "Developer & IDE", "#1D2021", "#EBDBB2", "#282828", "#EBDBB2", "#282828", "#EBDBB2", "#3C3836", "#EBDBB2", "#504945", "#1D2021", "#141617", "#282828", "#665C54", "#7C6F64", "#FE8019", "#FABD2F", "#D65D0E", "#EBDBB2");
            Add("gruvbox_light", "Gruvbox Light", "Developer & IDE", "#FBF1C7", "#3C3836", "#F2E5BC", "#3C3836", "#F2E5BC", "#3C3836", "#FFFFFF", "#3C3836", "#EBDAB4", "#F2E5BC", "#EBDAB4", "#FFFFFF", "#D5C4A1", "#BDAE93", "#AF3A03", "#D65D0E", "#AF3A03", "#FFFFFF");
            Add("material_oceanic", "Material Oceanic", "Developer & IDE", "#1E272C", "#C7D0D9", "#263238", "#EEFFFF", "#263238", "#C7D0D9", "#314048", "#EEFFFF", "#3E515B", "#1E272C", "#151B1E", "#263238", "#455A64", "#546E7A", "#009688", "#80CBC4", "#009688", "#FFFFFF");
            Add("palenight", "Palenight", "Developer & IDE", "#202334", "#BFC7D5", "#292D3E", "#D6DEEB", "#292D3E", "#BFC7D5", "#33374D", "#D6DEEB", "#414763", "#202334", "#171925", "#292D3E", "#4E5579", "#676E95", "#82AAFF", "#C792EA", "#82AAFF", "#292D3E");
            Add("horizon_dark", "Horizon Dark", "Developer & IDE", "#17181F", "#D5D8DA", "#1C1E26", "#ECEFF1", "#1C1E26", "#D5D8DA", "#252834", "#ECEFF1", "#333747", "#17181F", "#101116", "#1C1E26", "#40455A", "#535A75", "#E95678", "#F09383", "#E95678", "#FFFFFF");
            Add("cobalt2_blue", "Cobalt2 Blue", "Developer & IDE", "#132738", "#FFFFFF", "#193549", "#FFFFFF", "#193549", "#FFFFFF", "#224762", "#FFFFFF", "#2C5B7D", "#132738", "#0D1B27", "#193549", "#356E97", "#4487B8", "#FFC600", "#FF9D00", "#0088FF", "#FFFFFF");
            Add("synthwave_84", "Synthwave '84", "Developer & IDE", "#241B2F", "#F92AAD", "#2A2139", "#F92AAD", "#2A2139", "#F92AAD", "#372B4B", "#36F9F6", "#493A64", "#241B2F", "#1B1423", "#2A2139", "#584678", "#725A9B", "#FEDE5D", "#FE4450", "#725A9B", "#36F9F6");

            // =========================================================================
            // CATEGORY 5: VIBRANT, NEON & CYBERPUNK (15 THEMES)
            // =========================================================================
            Add("cyberpunk_neon", "Cyberpunk 2077", "Neon & Cyberpunk", "#0D0B18", "#00F0FF", "#15102A", "#00F0FF", "#15102A", "#00F0FF", "#1C1438", "#00F0FF", "#FF007F", "#15102A", "#080610", "#1C1438", "#FF007F", "#00F0FF", "#FF007F", "#DA006C", "#FF007F", "#FFFFFF");
            Add("synthwave_neon", "Synthwave Neon", "Neon & Cyberpunk", "#120B1C", "#FF71CE", "#1B102B", "#01CDFE", "#1B102B", "#FF71CE", "#281840", "#01CDFE", "#3B235D", "#1B102B", "#0A0610", "#281840", "#05FFA1", "#B967FF", "#01CDFE", "#05FFA1", "#B967FF", "#FFFFFF");
            Add("electric_violet", "Electric Violet", "Neon & Cyberpunk", "#10061A", "#E0AAFF", "#1A0B2B", "#F72585", "#1A0B2B", "#E0AAFF", "#2A1245", "#F72585", "#3E1B66", "#1A0B2B", "#0A0410", "#2A1245", "#7209B7", "#4CC9F0", "#F72585", "#7209B7", "#7209B7", "#FFFFFF");
            Add("cyber_blue", "Cyber Blue", "Neon & Cyberpunk", "#050F1A", "#00F5FF", "#0A1D30", "#00F5FF", "#0A1D30", "#00F5FF", "#102C48", "#FFFFFF", "#184068", "#0A1D30", "#02070D", "#102C48", "#00A8FF", "#00F5FF", "#00A8FF", "#0088CC", "#00A8FF", "#050F1A");
            Add("matrix_rain", "Matrix Digital Rain", "Neon & Cyberpunk", "#030A05", "#00FF66", "#06150A", "#00FF66", "#06150A", "#00FF66", "#0C2513", "#00FF66", "#143C1F", "#06150A", "#010402", "#0C2513", "#00BB44", "#00FF66", "#00FF66", "#00CC55", "#008833", "#030A05");
            Add("miami_vice", "Miami Vice Pink", "Neon & Cyberpunk", "#120B18", "#FF599C", "#1C1226", "#00E5FF", "#1C1226", "#FF599C", "#2A1C39", "#00E5FF", "#3E2954", "#1C1226", "#0B070F", "#2A1C39", "#FF599C", "#00E5FF", "#FF599C", "#E03F80", "#00E5FF", "#120B18");
            Add("laser_lime", "Laser Lime", "Neon & Cyberpunk", "#0B1207", "#CCFF00", "#121F0D", "#CCFF00", "#121F0D", "#CCFF00", "#1C3114", "#CCFF00", "#28461D", "#121F0D", "#060A04", "#1C3114", "#A2CC00", "#CCFF00", "#CCFF00", "#A2CC00", "#789900", "#0B1207");
            Add("plasma_magenta", "Plasma Magenta", "Neon & Cyberpunk", "#140614", "#FF00AA", "#210A21", "#FF66CC", "#210A21", "#FF00AA", "#331033", "#FF66CC", "#4A184A", "#210A21", "#0B030B", "#331033", "#CC0088", "#FF00AA", "#FF00AA", "#CC0088", "#AA0077", "#FFFFFF");
            Add("acid_yellow", "Acid Yellow", "Neon & Cyberpunk", "#121206", "#FFEE00", "#1F1F0B", "#FFEE00", "#1F1F0B", "#FFEE00", "#303012", "#FFEE00", "#45451A", "#1F1F0B", "#0A0A03", "#303012", "#CCCC00", "#FFEE00", "#FFEE00", "#CCCC00", "#999900", "#121206");
            Add("ultraviolet", "Ultraviolet", "Neon & Cyberpunk", "#0F061C", "#D100D1", "#190B2E", "#FF33FF", "#190B2E", "#D100D1", "#28124A", "#FF33FF", "#3B1B6D", "#190B2E", "#08030F", "#28124A", "#AA00AA", "#D100D1", "#FF007F", "#D100D1", "#800080", "#FFFFFF");
            Add("neon_tokyo", "Neon Tokyo", "Neon & Cyberpunk", "#0A0A17", "#00E5FF", "#121226", "#FF0055", "#121226", "#00E5FF", "#1C1C3B", "#FFFFFF", "#292956", "#121226", "#06060E", "#1C1C3B", "#FF0055", "#00E5FF", "#00E5FF", "#00B4D8", "#FF0055", "#FFFFFF");
            Add("hyperdrive", "Hyperdrive", "Neon & Cyberpunk", "#040B17", "#00F0FF", "#08152B", "#7000FF", "#08152B", "#00F0FF", "#0E2448", "#FFFFFF", "#16376D", "#08152B", "#02060D", "#0E2448", "#00F0FF", "#7000FF", "#7000FF", "#5900CC", "#00F0FF", "#040B17");
            Add("retrowave_sunset", "Retrowave Sunset", "Neon & Cyberpunk", "#1A091A", "#FF8800", "#260E26", "#FF0080", "#260E26", "#FF8800", "#3B163B", "#FFEE00", "#542054", "#260E26", "#0E050E", "#3B163B", "#FF0080", "#FF8800", "#FF0080", "#D6006B", "#FF8800", "#FFFFFF");
            Add("quantum_blue", "Quantum Blue", "Neon & Cyberpunk", "#030E1F", "#38BDF8", "#071B38", "#67E8F9", "#071B38", "#38BDF8", "#0D2C59", "#FFFFFF", "#154385", "#071B38", "#020710", "#0D2C59", "#0284C7", "#38BDF8", "#0284C7", "#0369A1", "#0284C7", "#FFFFFF");
            Add("vaporwave_pastel", "Vaporwave Pastel", "Neon & Cyberpunk", "#1C132B", "#FF99C8", "#2A1D3D", "#A9DEF9", "#2A1D3D", "#FF99C8", "#3E2B58", "#FCF6BD", "#563D78", "#2A1D3D", "#110B1C", "#3E2B58", "#D0F4DE", "#E4C1F9", "#FF99C8", "#FF70A6", "#E4C1F9", "#1C132B");

            // =========================================================================
            // CATEGORY 6: ROYAL, LUXURY & ACCENT (14 THEMES)
            // =========================================================================
            Add("royal_amethyst", "Royal Amethyst", "Royal & Luxury", "#120B1C", "#E9D5FF", "#1D122E", "#F3E8FF", "#1D122E", "#E9D5FF", "#2E1D48", "#F3E8FF", "#432B68", "#1D122E", "#0B0711", "#2E1D48", "#6B21A8", "#9333EA", "#9333EA", "#7E22CE", "#7E22CE", "#FFFFFF");
            Add("imperial_gold", "Imperial Gold", "Royal & Luxury", "#14120B", "#FDE68A", "#211E13", "#FEF3C7", "#211E13", "#FDE68A", "#332F1E", "#FEF3C7", "#4A442C", "#211E13", "#0C0B07", "#332F1E", "#CA8A04", "#EAB308", "#EAB308", "#CA8A04", "#A16207", "#FFFFFF");
            Add("velvet_crimson", "Velvet Crimson", "Royal & Luxury", "#180A0C", "#FECDD3", "#261014", "#FFE4E6", "#261014", "#FECDD3", "#3B1A20", "#FFE4E6", "#55262F", "#261014", "#0E0607", "#3B1A20", "#BE123C", "#E11D48", "#E11D48", "#BE123C", "#9F1239", "#FFFFFF");
            Add("sapphire_jewel", "Sapphire Jewel", "Royal & Luxury", "#06101E", "#BAE6FD", "#0B1D36", "#E0F2FE", "#0B1D36", "#BAE6FD", "#133159", "#E0F2FE", "#1D477F", "#0B1D36", "#030810", "#133159", "#0369A1", "#0284C7", "#0284C7", "#0369A1", "#075985", "#FFFFFF");
            Add("rose_gold", "Rose Gold", "Royal & Luxury", "#1A1014", "#FBCFE8", "#291A20", "#FDF2F8", "#291A20", "#FBCFE8", "#3E2831", "#FDF2F8", "#593A47", "#291A20", "#0F090B", "#3E2831", "#DB2777", "#F472B6", "#F43F5E", "#E11D48", "#DB2777", "#FFFFFF");
            Add("champagne_luxe", "Champagne Luxe", "Royal & Luxury", "#FBF9F4", "#3D372E", "#F3EDE1", "#3D372E", "#F3EDE1", "#3D372E", "#FFFFFF", "#3D372E", "#E4D9C7", "#F3EDE1", "#E4D9C7", "#FFFFFF", "#D1C3AD", "#B8A589", "#D97706", "#B45309", "#D97706", "#FFFFFF");
            Add("ruby_red", "Ruby Red", "Royal & Luxury", "#19080A", "#FFA6B6", "#280E12", "#FFCCD5", "#280E12", "#FFA6B6", "#3E171D", "#FFCCD5", "#5A232B", "#280E12", "#0F0506", "#3E171D", "#E60039", "#FF2E63", "#FF2E63", "#E60039", "#B3002D", "#FFFFFF");
            Add("emerald_jewel", "Emerald Jewel", "Royal & Luxury", "#051610", "#6EE7B7", "#0A251B", "#A7F3D0", "#0A251B", "#6EE7B7", "#113A2B", "#A7F3D0", "#1B543E", "#0A251B", "#030D09", "#113A2B", "#059669", "#10B981", "#10B981", "#059669", "#047857", "#FFFFFF");
            Add("midnight_bronze", "Midnight Bronze", "Royal & Luxury", "#14110F", "#FED7AA", "#211D19", "#FFEDD5", "#211D19", "#FED7AA", "#332D27", "#FFEDD5", "#4B423A", "#211D19", "#0B0908", "#332D27", "#9A3412", "#C2410C", "#C2410C", "#9A3412", "#7C2D12", "#FFFFFF");
            Add("celestial_turquoise", "Celestial Turquoise", "Royal & Luxury", "#051518", "#5EEAD4", "#0A2429", "#99F6E4", "#0A2429", "#5EEAD4", "#123A41", "#99F6E4", "#1B535C", "#0A2429", "#030C0E", "#123A41", "#0D9488", "#14B8A6", "#14B8A6", "#0D9488", "#0F766E", "#051518");
            Add("sovereign_purple", "Sovereign Purple", "Royal & Luxury", "#140A21", "#DDD6FE", "#201134", "#EDE9FE", "#201134", "#DDD6FE", "#321C50", "#EDE9FE", "#4A2B73", "#201134", "#0B0512", "#321C50", "#7C3AED", "#8B5CF6", "#8B5CF6", "#7C3AED", "#6D28D9", "#FFFFFF");
            Add("platinum_azure", "Platinum Azure", "Royal & Luxury", "#0A131F", "#BAE6FD", "#102033", "#E0F2FE", "#102033", "#BAE6FD", "#1B324F", "#E0F2FE", "#27476F", "#102033", "#060A10", "#1B324F", "#0284C7", "#38BDF8", "#0284C7", "#0369A1", "#075985", "#FFFFFF");
            Add("venetian_copper", "Venetian Copper", "Royal & Luxury", "#170F0A", "#FFEDD5", "#241811", "#FFF7ED", "#241811", "#FFEDD5", "#38271D", "#FFF7ED", "#50392B", "#241811", "#0D0805", "#38271D", "#B45309", "#D97706", "#D97706", "#B45309", "#92400E", "#FFFFFF");
            Add("garnet_night", "Garnet Night", "Royal & Luxury", "#16070B", "#FFE4E6", "#240D13", "#FFF1F2", "#240D13", "#FFE4E6", "#38161E", "#FFF1F2", "#52222D", "#240D13", "#0D0406", "#38161E", "#BE123C", "#E11D48", "#E11D48", "#BE123C", "#9F1239", "#FFFFFF");

            // Total: 20 + 20 + 18 + 18 + 15 + 14 = 105 PRESET THEMES!
        }

        private static void Add(
            string id, string name, string category,
            string winBg, string winFg,
            string headerBg, string headerFg,
            string menuBg, string menuFg,
            string submenuBg, string submenuFg,
            string hoverBg, string navBg,
            string canvasBg, string cardBg,
            string borderDef, string borderLt,
            string accentDef, string accentHov,
            string statusBg, string statusFg)
        {
            _themes.Add(ThemeDefinition.Create(
                id, name, category,
                winBg, winFg,
                headerBg, headerFg,
                menuBg, menuFg,
                submenuBg, submenuFg,
                hoverBg, navBg,
                canvasBg, cardBg,
                borderDef, borderLt,
                accentDef, accentHov,
                statusBg, statusFg));
        }
    }
}
