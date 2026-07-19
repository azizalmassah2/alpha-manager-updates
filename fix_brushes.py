import os, re

path = r'd:\LUXCARD\desktop\MikroTikVoucherPrinter.UI\Views'

for root, dirs, files in os.walk(path):
    for f in files:
        if f.endswith('.xaml'):
            filepath = os.path.join(root, f)
            with open(filepath, 'r', encoding='utf-8') as file:
                content = file.read()
            original = content

            # Fix TemplateBinding that got corrupted:
            # {TemplateBinding MaterialDesignDivider} -> {TemplateBinding BorderBrush}
            content = re.sub(r'\{TemplateBinding MaterialDesignDivider\}', '{TemplateBinding BorderBrush}', content)
            # Fix attached property syntax: materialDesign:MaterialDesignDivider (if any)
            # Fix any remaining attribute-name corruption like CornerRadius.MaterialDesignDivider
            content = re.sub(r'CornerRadius\.MaterialDesignDivider', 'CornerRadius.BorderBrush', content)
            # TargetName="MaterialDesignDivider" in triggers - this would be a named element
            # Restore Style Property="MaterialDesignDivider" that wasn't caught
            content = re.sub(r'Property="MaterialDesignDivider"', 'Property="BorderBrush"', content)

            if content != original:
                with open(filepath, 'w', encoding='utf-8') as file:
                    file.write(content)
                print(f'Fixed: {filepath}')

print('Done')
