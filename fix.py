import codecs
lines = codecs.open("d:/LUXCARD/desktop/Lux.Management.Console/Modules/DbExplorer/ViewModels/DbExplorerViewModel.cs", "r", "utf-8").readlines()
lines[527] = '            System.Windows.MessageBox.Show("Disabled", "Info", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);\n'
codecs.open("d:/LUXCARD/desktop/Lux.Management.Console/Modules/DbExplorer/ViewModels/DbExplorerViewModel.cs", "w", "utf-8").writelines(lines)