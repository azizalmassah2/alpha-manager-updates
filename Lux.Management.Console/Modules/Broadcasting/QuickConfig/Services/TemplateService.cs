using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services
{
    public class TemplateService : ITemplateService
    {
        private string TemplatesDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");

        public TemplateService()
        {
            if (!Directory.Exists(TemplatesDirectory))
            {
                Directory.CreateDirectory(TemplatesDirectory);
            }
        }

        public async Task SaveTemplateAsync(DeviceTemplate template)
        {
            if (string.IsNullOrWhiteSpace(template.TemplateName))
            {
                throw new ArgumentException("اسم القالب لا يمكن أن يكون فارغاً.");
            }

            var filePath = Path.Combine(TemplatesDirectory, $"{template.TemplateName}.json");
            var json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<DeviceTemplate> LoadTemplateAsync(string name)
        {
            var filePath = Path.Combine(TemplatesDirectory, $"{name}.json");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"القالب {name} غير موجود.");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var template = JsonSerializer.Deserialize<DeviceTemplate>(json);
            if (template == null)
            {
                throw new Exception("فشل تحليل محتوى القالب.");
            }

            return template;
        }

        public Task DeleteTemplateAsync(string name)
        {
            var filePath = Path.Combine(TemplatesDirectory, $"{name}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Task.CompletedTask;
        }

        public async Task<List<DeviceTemplate>> GetAllTemplatesAsync()
        {
            var list = new List<DeviceTemplate>();
            if (Directory.Exists(TemplatesDirectory))
            {
                var files = Directory.GetFiles(TemplatesDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var template = JsonSerializer.Deserialize<DeviceTemplate>(json);
                        if (template != null)
                        {
                            list.Add(template);
                        }
                    }
                    catch { /* Ignore corrupt template files */ }
                }
            }
            return list;
        }
    }
}
