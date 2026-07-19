using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;
using OpenWrtProgrammerPro.Services.Interfaces;

namespace OpenWrtProgrammerPro.Services
{
    public class SavedNetworkService : ISavedNetworkService
    {
        private string SavedNetworksDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SavedNetworks");
        private string FilePath => Path.Combine(SavedNetworksDirectory, "networks.json");

        public SavedNetworkService()
        {
            if (!Directory.Exists(SavedNetworksDirectory))
            {
                Directory.CreateDirectory(SavedNetworksDirectory);
            }
        }

        public async Task<List<SavedNetwork>> GetAllNetworksAsync()
        {
            if (!File.Exists(FilePath))
            {
                return new List<SavedNetwork>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(FilePath);
                var list = JsonSerializer.Deserialize<List<SavedNetwork>>(json);
                return list ?? new List<SavedNetwork>();
            }
            catch
            {
                return new List<SavedNetwork>();
            }
        }

        public async Task SaveNetworkAsync(SavedNetwork network)
        {
            if (string.IsNullOrWhiteSpace(network.ProfileName))
            {
                throw new ArgumentException("اسم ملف التعريف لا يمكن أن يكون فارغاً.");
            }

            var list = await GetAllNetworksAsync();
            
            // Remove existing with same name if editing
            var existing = list.FirstOrDefault(n => n.ProfileName.Equals(network.ProfileName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                list.Remove(existing);
            }

            list.Add(network);

            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(FilePath, json);
        }

        public async Task DeleteNetworkAsync(string profileName)
        {
            var list = await GetAllNetworksAsync();
            var existing = list.FirstOrDefault(n => n.ProfileName.Equals(profileName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                list.Remove(existing);
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(FilePath, json);
            }
        }
    }
}
