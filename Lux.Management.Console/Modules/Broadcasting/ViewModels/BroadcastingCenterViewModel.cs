using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Core;
using System.Collections.Generic;

namespace Lux.Management.Console.Modules.Broadcasting.ViewModels
{
    public class BroadcastingNavigationNode
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public object? ViewModel { get; set; }
        public string? ParentTitle { get; set; }
        public List<BroadcastingNavigationNode> Children { get; set; } = new();
    }

    public partial class BroadcastingCenterViewModel : ViewModelBase
    {
        public BroadcastingNeighborsViewModel NeighborsVM { get; }
        public BroadcastingMaintenanceViewModel MaintenanceVM { get; }
        public BroadcastingFlashingViewModel FlashingVM { get; }

        [ObservableProperty]
        private object? _currentSubPageViewModel;

        [ObservableProperty]
        private List<BroadcastingNavigationNode> _navigationTree = new();

        [ObservableProperty]
        private BroadcastingNavigationNode? _selectedSection;

        [ObservableProperty]
        private BroadcastingNavigationNode? _selectedSubNode;

        [ObservableProperty]
        private List<BroadcastingNavigationNode> _activeSectionPills = new();

        [ObservableProperty]
        private bool _isPillStripVisible;

        [ObservableProperty]
        private string _breadcrumbText = "أجهزة البث";

        [ObservableProperty]
        private bool _isNavPanelOpen;

        [RelayCommand]
        private void ToggleNavPanel() => IsNavPanelOpen = !IsNavPanelOpen;

        [RelayCommand]
        private void CloseNavPanel() => IsNavPanelOpen = false;

        partial void OnSelectedSectionChanged(BroadcastingNavigationNode? value)
        {
            if (value == null) return;
            ActiveSectionPills = value.Children;
            IsPillStripVisible = value.Children != null && value.Children.Count > 0;
            if (IsPillStripVisible)
                SelectedSubNode = value.Children[0];
            else
                SelectedSubNode = value;
            IsNavPanelOpen = false;
        }

        partial void OnSelectedSubNodeChanged(BroadcastingNavigationNode? value)
        {
            if (value?.ViewModel != null)
            {
                CurrentSubPageViewModel = value.ViewModel;
                UpdateBreadcrumb(value);

                if (value.ViewModel is IActivatable activatable)
                {
                    _ = SafeActivateAsync(activatable, value.Title);
                }
            }
        }

        private async Task SafeActivateAsync(IActivatable vm, string pageName)
        {
            try
            {
                await vm.ActivateAsync();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[⚠️] SafeActivateAsync failed for '{pageName}': {ex.Message}");
            }
        }

        private void UpdateBreadcrumb(BroadcastingNavigationNode node)
        {
            var segments = new List<string> { "أجهزة البث" };
            if (!string.IsNullOrEmpty(node.ParentTitle))
                segments.Add(node.ParentTitle);
            segments.Add(node.Title);
            BreadcrumbText = string.Join("  ❯  ", segments);
        }

        private void BuildNavigationTree()
        {
            // 1. النيبورز — كشف الأجهزة المحلية
            var neighbors = new BroadcastingNavigationNode
            {
                Title = "النيبورز",
                Description = "كشف الأجهزة المتصلة بالشبكة المحلية",
                Icon = "📡",
                ViewModel = NeighborsVM
            };

            // 2. الإعداد والصيانة — مع تبويبات فرعية
            var maintenance = new BroadcastingNavigationNode
            {
                Title = "الإعداد والصيانة",
                Description = "إدارة أجهزة البث عبر SSH وUSB",
                Icon = "🔧"
            };
            maintenance.Children.Add(new BroadcastingNavigationNode
            {
                Title = "الأجهزة المسجلة",
                Icon = "🖥️",
                ViewModel = MaintenanceVM,
                ParentTitle = "الإعداد والصيانة"
            });
            maintenance.Children.Add(new BroadcastingNavigationNode
            {
                Title = "الضبط السريع",
                Icon = "⚡",
                ViewModel = new QuickConfig.ViewModels.MainViewModel(),
                ParentTitle = "الإعداد والصيانة"
            });

            // 3. تفليش الأنظمة
            var flashing = new BroadcastingNavigationNode
            {
                Title = "تفليش الأنظمة",
                Description = "تحديث وتفليش أجهزة البث",
                Icon = "💾",
                ViewModel = FlashingVM
            };

            NavigationTree = new List<BroadcastingNavigationNode>
            {
                neighbors,
                maintenance,
                flashing
            };
        }

        public BroadcastingCenterViewModel(
            IPermissionService permissionService,
            IEventBus eventBus,
            BroadcastingNeighborsViewModel neighborsVM,
            BroadcastingMaintenanceViewModel maintenanceVM,
            BroadcastingFlashingViewModel flashingVM)
            : base(permissionService, eventBus)
        {
            Title = "مركز أجهزة البث";
            NeighborsVM = neighborsVM;
            MaintenanceVM = maintenanceVM;
            FlashingVM = flashingVM;
            BuildNavigationTree();

            // الاشتراك في حدث الانتقال عند حفظ الجهاز
            NeighborsVM.RequestNavigateToMaintenance += () =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var maintenanceNode = NavigationTree.Find(n => n.Title == "الإعداد والصيانة");
                    if (maintenanceNode != null && maintenanceNode.Children.Count > 0)
                    {
                        SelectedSection = maintenanceNode;
                        SelectedSubNode = maintenanceNode.Children[0]; // الأجهزة المسجلة
                    }
                });
            };
        }
    }
}
