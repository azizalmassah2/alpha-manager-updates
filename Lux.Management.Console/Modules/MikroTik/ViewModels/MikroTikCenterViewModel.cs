using Lux.Management.Console.ViewModels;
using Lux.Management.Console.Core.Security.Authorization;
using Lux.Management.Console.Core.Security.Models;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Modules.MikroTik.Dashboard;
using Lux.Management.Console.Modules.MikroTik.Hotspot.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager.Profiles.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager.Agents.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager.Printing.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager.Sales;
using Lux.Management.Console.Modules.MikroTik.Connections.ViewModels;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;
using Lux.Management.Console.Modules.Settings.ViewModels;
using Lux.Management.Console.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Lux.Management.Console.Modules.MikroTik.ViewModels
{
    public class PlaceholderViewModel
    {
        public string PageName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public PlaceholderViewModel(string pageName, string icon)
        {
            PageName = pageName;
            Icon = icon;
        }
    }

    public class NavigationNode
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public object? ViewModel { get; set; }
        public string? ParentTitle { get; set; }
        public System.Collections.Generic.List<NavigationNode> Children { get; set; } = new();
    }

    public partial class MikroTikCenterViewModel : ViewModelBase
    {
        public DashboardViewModel DashboardVM { get; }
        public ConnectionsViewModel ConnectionsVM { get; }
        public RouterManagementCenterViewModel RouterManagementCenterVM { get; }
        public VoucherManagementViewModel VouchersVM { get; }
        public SalesViewModel SalesVM { get; }
        public ProfileManagementViewModel ProfilesVM { get; }
        public AgentManagementViewModel AgentsVM { get; }
        public TemplateManagementViewModel TemplatesVM { get; }
        public DbExplorerViewModel DbExplorerVM { get; }
        public HotspotLoginViewModel HotspotLoginVM { get; }
        private readonly IShellState _shellState;
        private readonly IFeatureAuthorizationService _featureAuthorizationService;
        private NavigationNode? _previousSection;

        [ObservableProperty]
        private object? _currentSubPageViewModel;

        [ObservableProperty]
        private System.Collections.Generic.List<NavigationNode> _navigationTree = new();

        [ObservableProperty]
        private NavigationNode? _selectedSection;

        [ObservableProperty]
        private NavigationNode? _selectedSubNode;

        [ObservableProperty]
        private System.Collections.Generic.List<NavigationNode> _activeSectionPills = new();

        [ObservableProperty]
        private bool _isPillStripVisible;

        [ObservableProperty]
        private string _breadcrumbText = "المايكروتيك";

        // Hybrid Navigation: On-Demand panel visibility
        [ObservableProperty]
        private bool _isNavPanelOpen;

        // ── Commands ─────────────────────────────────────────────────────────
        [RelayCommand]
        private void ToggleNavPanel() => IsNavPanelOpen = !IsNavPanelOpen;

        [RelayCommand]
        private void CloseNavPanel() => IsNavPanelOpen = false;

        // ── Section and Sub-Node selection ───────────────────────────────────
        partial void OnSelectedSectionChanged(NavigationNode? value)
        {
            if (value == null) return;

            FeatureId? requiredFeature = value.Title switch
            {
                "إدارة الراوتر" => FeatureId.RouterManagement,
                "الفيلانات" => FeatureId.VlanManagement,
                "مراقبة الشبكة" => FeatureId.NetworkMonitoring,
                "صفحة تسجيل الدخول" => FeatureId.HotspotLoginEditor,
                _ => null
            };

            if (requiredFeature.HasValue && !_featureAuthorizationService.HasFeature(requiredFeature.Value))
            {
                System.Windows.MessageBox.Show(
                    $"ميزة '{value.Title}' غير متوفرة في النسخة المجانية.\nيرجى تفعيل البرنامج للوصول لكافة الميزات.",
                    "ميزة مقفلة",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);

                // إرجاع التحديد إلى القسم السابق أو الافتراضي (User Manager)
                var safeSection = _previousSection ?? NavigationTree.FirstOrDefault(n => n.Title == "User Manager");
                SelectedSection = safeSection;
                return;
            }

            _previousSection = value;

            ActiveSectionPills = value.Children;
            IsPillStripVisible = value.Children != null && value.Children.Count > 0;

            if (IsPillStripVisible)
            {
                // Default to first sub-page
                SelectedSubNode = value.Children[0];
            }
            else
            {
                // Directly navigate to section VM
                SelectedSubNode = value;
            }

            // Automatically close the overlay panel after selection
            IsNavPanelOpen = false;
        }

        partial void OnSelectedSubNodeChanged(NavigationNode? value)
        {
            if (value == null) return;

            // ── أوقف الـ polling على الشاشة السابقة قبل الانتقال ──
            if (CurrentSubPageViewModel is IDeactivatable previousDeactivatable)
                previousDeactivatable.Deactivate();

            if (value.ViewModel != null)
            {
                CurrentSubPageViewModel = value.ViewModel;
                UpdateBreadcrumb(value);

                // [PHASE-2] استخدام IActivatable — Lazy Loading آمن وموحد
                if (value.ViewModel is IActivatable activatable)
                    _ = SafeActivateAsync(activatable, value.Title);
            }
        }

        private async Task SafeActivateAsync(IActivatable vm, string pageName)
        {
            try
            {
                await vm.ActivateAsync();
            }
            catch (Exception ex)
            {
                // لا نعطل التنقل إذا فشل التحميل
                System.Diagnostics.Debug.WriteLine($"[⚠️] SafeActivateAsync failed for '{pageName}': {ex.Message}");
            }
        }

        private void UpdateBreadcrumb(NavigationNode node)
        {
            var segments = new System.Collections.Generic.List<string> { "المايكروتيك" };
            if (!string.IsNullOrEmpty(node.ParentTitle))
            {
                segments.Add(node.ParentTitle);
            }
            segments.Add(node.Title);
            BreadcrumbText = string.Join("  ❯  ", segments);
        }

        private void BuildNavigationTree()
        {
            // 1. Router Management
            var routerMgmt = new NavigationNode 
            { 
                Title = "إدارة الراوتر", 
                Description = "إدارة إعدادات الميكروتيك والشبكة", 
                Icon = "🔌" 
            };
            routerMgmt.Children.Add(new NavigationNode { Title = "لوحة المعلومات",   Icon = "📋", ViewModel = RouterManagementCenterVM.DashboardVM,  ParentTitle = "إدارة الراوتر" });
            routerMgmt.Children.Add(new NavigationNode { Title = "الموارد",          Icon = "💻", ViewModel = RouterManagementCenterVM.ResourcesVM,   ParentTitle = "إدارة الراوتر" });
            routerMgmt.Children.Add(new NavigationNode { Title = "الواجهات",         Icon = "🔌", ViewModel = RouterManagementCenterVM.InterfacesVM,  ParentTitle = "إدارة الراوتر" });
            routerMgmt.Children.Add(new NavigationNode { Title = "عناوين IP",        Icon = "🌐", ViewModel = RouterManagementCenterVM.IpAddressesVM, ParentTitle = "إدارة الراوتر" });
            routerMgmt.Children.Add(new NavigationNode { Title = "DNS",              Icon = "🔎", ViewModel = RouterManagementCenterVM.DnsVM,         ParentTitle = "إدارة الراوتر" });
            routerMgmt.Children.Add(new NavigationNode { Title = "التوجيه",          Icon = "🛣️", ViewModel = RouterManagementCenterVM.RoutesVM,      ParentTitle = "إدارة الراوتر" });
            routerMgmt.Children.Add(new NavigationNode { Title = "النسخ الاحتياطي", Icon = "💾", ViewModel = RouterManagementCenterVM.BackupsVM,     ParentTitle = "إدارة الراوتر" });
            routerMgmt.Children.Add(new NavigationNode { Title = "العمليات",         Icon = "⚙️", ViewModel = RouterManagementCenterVM.OperationsVM,  ParentTitle = "إدارة الراوتر" });

            // 1.1 Standalone NOC section (Standalone main category)
            var nocNode = new NavigationNode
            {
                Title = "الفيلانات",
                Description = "مراقبة حية للفيلانات والسرعات والأحمال والتنبيهات",
                Icon = "📡",
                ViewModel = RouterManagementCenterVM.NocVM
            };

            // 2. User Manager
            var userManager  = new NavigationNode 
            { 
                Title = "User Manager", 
                Description = "إدارة المستخدمين والباقات والكروت", 
                Icon = "👥" 
            };
            userManager.Children.Add(new NavigationNode { Title = "المبيعات",          Icon = "📈", ViewModel = SalesVM,    ParentTitle = "User Manager" });
            userManager.Children.Add(new NavigationNode { Title = "الكروت",           Icon = "🎫", ViewModel = VouchersVM,  ParentTitle = "User Manager" });
            userManager.Children.Add(new NavigationNode { Title = "الباقات",          Icon = "📦", ViewModel = ProfilesVM,  ParentTitle = "User Manager" });
            userManager.Children.Add(new NavigationNode { Title = "الوكلاء",          Icon = "🤝", ViewModel = AgentsVM,    ParentTitle = "User Manager" });
            userManager.Children.Add(new NavigationNode { Title = "الطباعة والقوالب", Icon = "🖨️", ViewModel = TemplatesVM, ParentTitle = "User Manager" });

            // 3. Print Templates
            var printTemplates = new NavigationNode 
            { 
                Title = "قوالب الطباعة", 
                Description = "تصميم وإعداد إيصالات الدفع والفواتير", 
                Icon = "🖨️" 
            };
            printTemplates.Children.Add(new NavigationNode { Title = "إيصالات الدفع", Icon = "🧾", ViewModel = new PlaceholderViewModel("إيصالات الدفع", "🧾"), ParentTitle = "قوالب الطباعة" });
            printTemplates.Children.Add(new NavigationNode { Title = "الفواتير",       Icon = "💵", ViewModel = new PlaceholderViewModel("الفواتير", "💵"),     ParentTitle = "قوالب الطباعة" });

            // 4. Network Monitoring (Renamed to VLANs)
            var netMonitoring = new NavigationNode 
            { 
                Title = "الفيلانات (الشبكة)", 
                Description = "مراقبة الأداء وأجهزة الشبكة الفعالة", 
                Icon = "📡" 
            };
            netMonitoring.Children.Add(new NavigationNode { Title = "الأجهزة",     Icon = "🖥️", ViewModel = new PlaceholderViewModel("الأجهزة", "🖥️"),       ParentTitle = "مراقبة الشبكة" });
            netMonitoring.Children.Add(new NavigationNode { Title = "التنبيهات",   Icon = "🔔", ViewModel = new PlaceholderViewModel("التنبيهات", "🔔"),     ParentTitle = "مراقبة الشبكة" });
            netMonitoring.Children.Add(new NavigationNode { Title = "السجلات",     Icon = "📝", ViewModel = new PlaceholderViewModel("السجلات", "📝"),       ParentTitle = "مراقبة الشبكة" });
            netMonitoring.Children.Add(new NavigationNode { Title = "الإحصائيات", Icon = "📈", ViewModel = new PlaceholderViewModel("الإحصائيات", "📈"),   ParentTitle = "مراقبة الشبكة" });

            // 5. Database Explorer
            var dbExplorer = new NavigationNode 
            { 
                Title = "مستكشف البيانات", 
                Description = "عرض وتصدير بيانات الجداول المحلية", 
                Icon = "🗄️", 
                ViewModel = DbExplorerVM 
            };

            // 6. Hotspot Login Editor
            var hotspotLogin = new NavigationNode
            {
                Title = "صفحة تسجيل الدخول",
                Description = "تعديل وتخصيص صفحة تسجيل الدخول للشبكة ورفعها للروتر",
                Icon = "🔑",
                ViewModel = HotspotLoginVM
            };

            NavigationTree = new System.Collections.Generic.List<NavigationNode>
            {
                routerMgmt,
                nocNode,
                userManager,
                printTemplates,
                netMonitoring,
                dbExplorer,
                hotspotLogin
            };
        }

        public MikroTikCenterViewModel(
            IPermissionService permissionService, 
            IEventBus eventBus,
            IShellState shellState,
            DashboardViewModel dashboardVM,
            ConnectionsViewModel connectionsVM,
            RouterManagementCenterViewModel routerManagementCenterVM,
            VoucherManagementViewModel vouchersVM,
            SalesViewModel salesVM,
            ProfileManagementViewModel profilesVM,
            AgentManagementViewModel agentsVM,
            TemplateManagementViewModel templatesVM,
            DbExplorerViewModel dbExplorerVM,
            HotspotLoginViewModel hotspotLoginVM,
            IFeatureAuthorizationService featureAuthorizationService) 
            : base(permissionService, eventBus)
        {
            Title = "مركز المايكروتيك";
            _shellState = shellState;
            _featureAuthorizationService = featureAuthorizationService;
            DashboardVM = dashboardVM;
            ConnectionsVM = connectionsVM;
            RouterManagementCenterVM = routerManagementCenterVM;
            VouchersVM = vouchersVM;
            SalesVM = salesVM;
            ProfilesVM = profilesVM;
            AgentsVM = agentsVM;
            TemplatesVM = templatesVM;
            DbExplorerVM = dbExplorerVM;
            HotspotLoginVM = hotspotLoginVM;

            BuildNavigationTree();

            // [PHASE-2] تم حذف تعيين SelectedSection من Constructor —
            // سيتم تفعيله بعد ظهور الواجهة عبر MikroTikCenterPage.Loaded event
            // هذا يمنع تحميل 30k سجل قبل ظهور أي شيء
        }
    }
}
