"""Generate the identical user guide as embedded RTF, TXT and PDF.
Requires Python 3, reportlab, and DejaVu fonts (PDF authoring only).
Run from any directory: python docs/generar_guia.py
The Windows app does not need Python or the fonts installed.
"""
from pathlib import Path
import json,re,html
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.platypus import SimpleDocTemplate,Paragraph,Spacer,PageBreak,KeepTogether
from reportlab.lib.pagesizes import A4
root=Path(__file__).resolve().parent.parent
chapters=json.loads((root/'docs/guia.json').read_text())
licenses=[('Anexo A · Licencia de la GUI','SeaChestGUI-MIT.txt'),('Anexo B · Licencias de openSeaChest','openSeaChest-v26.09-alpha-zbd-LICENSE.md'),('Anexo C · Fuentes del PDF','DejaVu-Fonts.txt')]
for title,name in licenses:
    raw=(root/'LICENCIAS'/name).read_text()
    blocks=[]
    for part in re.split(r'\n\s*\n',raw.strip()):
        if part.startswith('#'):
            blocks.append(['h',re.sub(r'^#+\s*','',part)])
        else:
            part=re.sub(r'^> ?', '', part, flags=re.M)
            part=re.sub(r'\*\*(.*?)\*\*',r'\1',part)
            blocks.append(['p',' '.join(part.split())])
    chapters.append({'title':title,'blocks':blocks,'license':True})
# RTF uses UTF-16 escape values, so accents render in .NET RichTextBox.
def esc(t):
    result=''
    for c in t:
        n=ord(c)
        if c in '\\{}':result+='\\'+c
        elif c=='\n':result+=r'\line '
        elif n>127:
            for i in range(0,len(c.encode('utf-16-le')),2):
                v=int.from_bytes(c.encode('utf-16-le')[i:i+2],'little');result+='\\u'+str(v if v<32768 else v-65536)+'?'
        else:result+=c
    return result
rtfs=[]
for ch in chapters:
    s=r'{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}{\colortbl;\red19\green35\blue52;\red0\green115\blue102;}\viewkind4\uc1\f0 '
    s+=r'\cf1\b\fs30 '+esc(ch['title'])+r'\b0\fs22\par\par '
    for kind,t in ch['blocks']:
        s+=(r'\cf2\b ' if kind in ('h','tip') else r'\cf1\b0 ')+esc(t)+r'\b0\cf1\par\par '
    rtfs.append(s+'}')
def cs(t):return '@"'+t.replace('"','""')+'"'
source='// Generated from docs/guia.json and LICENCIAS. Do not edit by hand.\nnamespace SeaChestGUI { internal static class GuideContent {\n'
source+='internal static readonly string[] Titles = new string[]{'+','.join(cs(c['title']) for c in chapters)+'};\n'
source+='internal static readonly string[] Rtf = new string[]{'+','.join(cs(s) for s in rtfs)+'};\n}}\n'
(root/'src/GuideContent.cs').write_text(source,encoding='utf-8')
(root/'docs/GUIA_DE_USO.txt').write_text('\n\n'.join(c['title']+'\n\n'+'\n\n'.join(b[1] for b in c['blocks']) for c in chapters))
fontdir=Path('/usr/share/fonts/truetype/dejavu')
pdfmetrics.registerFont(TTFont('DV',str(fontdir/'DejaVuSans.ttf')))
pdfmetrics.registerFont(TTFont('DVB',str(fontdir/'DejaVuSans-Bold.ttf')))
pdfmetrics.registerFontFamily('DV',normal='DV',bold='DVB')
navy=colors.HexColor('#132334');teal=colors.HexColor('#007366')
styles={
 'title':ParagraphStyle('title',fontName='DVB',fontSize=21,leading=25,textColor=navy,spaceAfter=15),
 'p':ParagraphStyle('p',fontName='DV',fontSize=10.2,leading=14.4,textColor=navy,spaceAfter=9),
 'h':ParagraphStyle('h',fontName='DVB',fontSize=11.3,leading=15,textColor=teal,spaceBefore=5,spaceAfter=6,keepWithNext=True),
 'tip':ParagraphStyle('tip',fontName='DV',fontSize=10,leading=14,textColor=navy,backColor=colors.HexColor('#EAF4F1'),borderPadding=9,spaceBefore=5,spaceAfter=15),
 'license':ParagraphStyle('license',fontName='DV',fontSize=8.7,leading=11.7,textColor=navy,spaceAfter=7)
}
def markup(t):
    t=html.escape(t)
    t=re.sub(r'https://[^\s<]+',lambda m:'<link href="'+m.group()+'" color="#007366">'+m.group()+'</link>',t)
    return t.replace('\n','<br/>')
class GuideDoc(SimpleDocTemplate):
    def afterFlowable(self,f):
        if isinstance(f,Paragraph) and f.style.name=='title':
            label=f.getPlainText();key='s'+str(self.page)
            self.canv.bookmarkPage(key);self.canv.addOutlineEntry(label,key,level=0)
def furniture(c,d):
    w,h=A4;c.saveState();c.setFillColor(navy);c.rect(0,h-36,w,36,fill=1,stroke=0)
    c.setFont('DVB',9);c.setFillColor(colors.white);c.drawString(43,h-23,'SeaChest GUI  /  GUÍA DE USO')
    c.setFont('DV',8);c.drawRightString(w-43,h-23,'LaNaveRETRO  ·  v0.1.8')
    c.setFillColor(teal);c.setFont('DV',8);c.drawString(43,25,'Interfaz independiente para openSeaChest  |  13/10/2026');c.drawRightString(w-43,25,str(d.page));c.restoreState()
pdf=root/'Guia_de_Uso_SeaChestGUI_v0.1.8.pdf'
doc=GuideDoc(str(pdf),pagesize=A4,rightMargin=43,leftMargin=43,topMargin=53,bottomMargin=43,title='Guía de Uso - SeaChest GUI v0.1.8',author='LaNaveRETRO',subject='Energía, diagnóstico y funciones de openSeaChest desde Windows')
story=[]
for i,ch in enumerate(chapters):
    if i:story.append(PageBreak())
    story.append(Paragraph(markup(ch['title']),styles['title']))
    for kind,t in ch['blocks']:
        st=styles['license'] if ch.get('license') and kind=='p' else styles[kind]
        story.append(Paragraph(markup(t),st))
doc.build(story,onFirstPage=furniture,onLaterPages=furniture)
print(pdf)
