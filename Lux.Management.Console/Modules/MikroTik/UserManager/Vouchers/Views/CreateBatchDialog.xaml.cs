using System.Windows;
using Microsoft.Extensions.Logging;
using Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.ViewModels;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.Views
{
    public partial class CreateBatchDialog : Window
    {
        private readonly CreateBatchDialogViewModel _viewModel;

        public CreateBatchDialog(
            IDbContextFactory<LuxCardDbContext> dbFactory,
            ISyncService syncService,
            IPrintService printService,
            ITemplateService templateService,
            ISettingsService settingsService,
            IActiveRouterContext activeRouterContext,
            IShellState shellState,
            ILogger logger,
            Lux.Management.Console.Core.Security.Authorization.IFeatureAuthorizationService featureAuthorizationService)
        {
            InitializeComponent();
            
            _viewModel = new CreateBatchDialogViewModel(
                dbFactory,
                syncService,
                printService,
                templateService,
                settingsService,
                activeRouterContext,
                shellState,
                logger,
                featureAuthorizationService);

            DataContext = _viewModel;
            _viewModel.RequestClose += (success) =>
            {
                Dispatcher.Invoke(() =>
                {
                    DialogResult = success;
                    Close();
                });
            };

            Loaded += CreateBatchDialog_Loaded;
        }

        private async void CreateBatchDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
