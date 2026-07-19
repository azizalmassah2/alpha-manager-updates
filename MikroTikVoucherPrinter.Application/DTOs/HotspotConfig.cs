using System;
using System.Collections.Generic;

namespace MikroTikVoucherPrinter.Application.DTOs
{
    public class HotspotConfig
    {
        public string SiteName { get; set; } = "◉ ألفّا بلاس ◉";
        public string WelcomeMessage { get; set; } = "مرحبا بكم في شبكة الفا لخدمات الانترنت";
        public bool WelcomeMessageV { get; set; } = false;
        public bool ErbV { get; set; } = false;
        public string TextSlider1 { get; set; } = "";
        public List<SpeedOptionDto> SpeedOptions { get; set; } = new();
        public string ImageCount { get; set; } = "0";
        public bool ImageV { get; set; } = false;
        public List<HotspotPackageDto> Packages { get; set; } = new();
        public string Offers { get; set; } = "لا توجد عروض حاليا";
        public List<string> SalesPoints { get; set; } = new();
        public string Estr { get; set; } = "";
        public string Moba { get; set; } = "";
        public string SupportPhone { get; set; } = "";
        public string DeveloperName { get; set; } = "م/ عزيز المساح";
        public string DeveloperPhone { get; set; } = "771122633";
        public string ActiveTheme { get; set; } = "sakura";
        public int themeHue { get; set; } = 217;
        public int themeS { get; set; } = 91;
        public int themeL { get; set; } = 60;
    }

    public class SpeedOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

    public class HotspotPackageDto
    {
        public string Vl { get; set; } = string.Empty; // Package name / validity
        public string Time { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
    }
}
