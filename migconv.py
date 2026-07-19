import os, glob, re
src='d:/LUXCARD/desktop/MikroTikVoucherPrinter.UI/Converters'
dst='d:/LUXCARD/desktop/Lux.Management.Console/Converters'
os.makedirs(dst, exist_ok=True)
for f in glob.glob(os.path.join(src, '*.cs')):
    dst_f = os.path.join(dst, os.path.basename(f))
    if not os.path.exists(dst_f):
        content = open(f, 'r', encoding='utf-8-sig').read()
        content = re.sub(r'namespace MikroTikVoucherPrinter\.UI\.Converters', 'namespace Lux.Management.Console.Converters', content)
        open(dst_f, 'w', encoding='utf-8').write(content)
