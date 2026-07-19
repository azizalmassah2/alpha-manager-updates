import os

file = r"d:\LUXCARD\desktop\Lux.Management.Console\MainWindow.xaml"

with open(file, 'r', encoding='utf-8') as f:
    content = f.read()

buttons = """                <Button Content="إدارة الكروت" Command="{Binding NavigateVouchersCommand}" Margin="0,5" Padding="10" Background="Transparent" BorderThickness="0" HorizontalContentAlignment="Right" Foreground="{DynamicResource TextBrush}"/>
                <Button Content="مركز الطباعة" Command="{Binding NavigatePrintCenterCommand}" Margin="0,5" Padding="10" Background="Transparent" BorderThickness="0" HorizontalContentAlignment="Right" Foreground="{DynamicResource TextBrush}"/>
                <Button Content="إدارة الملفات الشخصية" Command="{Binding NavigateProfilesCommand}" Margin="0,5" Padding="10" Background="Transparent" BorderThickness="0" HorizontalContentAlignment="Right" Foreground="{DynamicResource TextBrush}"/>
                <Button Content="إدارة الوكلاء" Command="{Binding NavigateAgentsCommand}" Margin="0,5" Padding="10" Background="Transparent" BorderThickness="0" HorizontalContentAlignment="Right" Foreground="{DynamicResource TextBrush}"/>
                <Button Content="إدارة القوالب" Command="{Binding NavigateTemplatesCommand}" Margin="0,5" Padding="10" Background="Transparent" BorderThickness="0" HorizontalContentAlignment="Right" Foreground="{DynamicResource TextBrush}"/>
                <Button Content="الإعدادات" Command="{Binding NavigateSettingsCommand}" Margin="0,5" Padding="10" Background="Transparent" BorderThickness="0" HorizontalContentAlignment="Right" Foreground="{DynamicResource TextBrush}"/>
"""

content = content.replace(
"""                <Button Content="ط¥ط¯ط§ط±ط© ط§ظ„ظƒط±ظˆطھ" Command="{Binding NavigateVouchersCommand}" Margin="0,5" Padding="10" Background="Transparent" BorderThickness="0" HorizontalContentAlignment="Right" Foreground="{DynamicResource TextBrush}"/>
                <Button Content="ظ…ط±ظƒط² ط§ظ„ط·ط¨ط§ط¹ط©" Command="{Binding NavigatePrintCenterCommand}" Margin="0,5" Padding="10" Background="Transparent" BorderThickness="0" HorizontalContentAlignment="Right" Foreground="{DynamicResource TextBrush}"/>""",
buttons)

with open(file, 'w', encoding='utf-8') as f:
    f.write(content)

print("Updated MainWindow.xaml buttons")
