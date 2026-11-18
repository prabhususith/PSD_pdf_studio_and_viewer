import os
import sys
from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.lib.units import inch
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Image, Table, TableStyle, PageBreak, KeepTogether, HRFlowable
)
from reportlab.pdfgen import canvas

# ----------------------------------------------------------------------
# PATHS & CONFIGURATION
# ----------------------------------------------------------------------
BASE_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FIGURES_DIR = os.path.join(BASE_DIR, "docs", "ebook_generator", "figures")
PDF_OUTPUT_PATH = os.path.join(BASE_DIR, "docs", "PSD_PDF_Studio_Master_Architecture_and_Engineering_EBook.pdf")
DIST_OUTPUT_PATH = os.path.join(BASE_DIR, "build", "dist", "PSD_PDF_Studio_Master_Architecture_and_Engineering_EBook.pdf")

# ----------------------------------------------------------------------
# OXFORD TWO-PASS NUMBERED CANVAS
# ----------------------------------------------------------------------
class OxfordNumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super(OxfordNumberedCanvas, self).__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_page_decorations(num_pages)
            super(OxfordNumberedCanvas, self).showPage()
        super(OxfordNumberedCanvas, self).save()

    def draw_page_decorations(self, total_pages):
        # Suppress running headers/footers on Cover Page (Page 1)
        if self._pageNumber <= 1:
            return

        self.saveState()
        self.setFont("Helvetica-Bold", 8)
        self.setFillColor(colors.HexColor("#002147")) # Oxford Navy

        # Running Header (Top)
        header_text = "PSD PDF STUDIO &amp; VIEWER • THE COMPLETE ARCHITECTURAL TREATISE"
        self.drawString(54, 752, header_text)
        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.75)
        self.line(54, 746, 558, 746)

        # Running Footer (Bottom)
        self.setFont("Helvetica", 8)
        self.setFillColor(colors.HexColor("#64748B"))
        self.drawString(54, 36, "Oxford Software Engineering Guild • Enterprise Edition 4.0 LTS")
        
        page_str = f"Page {self._pageNumber} of {total_pages}"
        self.drawRightString(558, 36, page_str)
        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.75)
        self.line(54, 46, 558, 46)

        self.restoreState()

# ----------------------------------------------------------------------
# STYLES FACTORY
# ----------------------------------------------------------------------
def create_oxford_styles():
    styles = getSampleStyleSheet()

    # Colors
    c_navy = colors.HexColor("#002147")
    c_crimson = colors.HexColor("#8A1538")
    c_slate = colors.HexColor("#1E293B")
    c_muted = colors.HexColor("#475569")
    c_code = colors.HexColor("#0F172A")

    # Headings
    styles.add(ParagraphStyle(
        name='OxfordTitle',
        fontName='Helvetica-Bold',
        fontSize=24,
        leading=28,
        textColor=c_navy,
        alignment=1, # Center
        spaceAfter=10
    ))

    styles.add(ParagraphStyle(
        name='OxfordSubtitle',
        fontName='Helvetica',
        fontSize=12,
        leading=16,
        textColor=c_crimson,
        alignment=1, # Center
        spaceAfter=20
    ))

    styles.add(ParagraphStyle(
        name='OxfordEpigraph',
        fontName='Helvetica-Oblique',
        fontSize=9.5,
        leading=14,
        textColor=c_muted,
        alignment=1, # Center
        spaceAfter=15
    ))

    styles.add(ParagraphStyle(
        name='ChapterTitle',
        fontName='Helvetica-Bold',
        fontSize=18,
        leading=22,
        textColor=c_navy,
        spaceBefore=12,
        spaceAfter=8,
        keepWithNext=True
    ))

    styles.add(ParagraphStyle(
        name='SectionTitle',
        fontName='Helvetica-Bold',
        fontSize=12.5,
        leading=16,
        textColor=c_crimson,
        spaceBefore=10,
        spaceAfter=4,
        keepWithNext=True
    ))

    styles.add(ParagraphStyle(
        name='SubsectionTitle',
        fontName='Helvetica-Bold',
        fontSize=10.5,
        leading=14,
        textColor=c_navy,
        spaceBefore=8,
        spaceAfter=3,
        keepWithNext=True
    ))

    styles.add(ParagraphStyle(
        name='OxfordBody',
        fontName='Helvetica',
        fontSize=9.5,
        leading=13.5,
        textColor=c_slate,
        spaceAfter=6
    ))

    styles.add(ParagraphStyle(
        name='OxfordBodyBold',
        fontName='Helvetica-Bold',
        fontSize=9.5,
        leading=13.5,
        textColor=c_slate,
        spaceAfter=6
    ))

    styles.add(ParagraphStyle(
        name='OxfordBullet',
        fontName='Helvetica',
        fontSize=9.0,
        leading=13.0,
        textColor=c_slate,
        leftIndent=15,
        firstLineIndent=-10,
        spaceAfter=3
    ))

    styles.add(ParagraphStyle(
        name='CodeStyle',
        fontName='Courier',
        fontSize=7.5,
        leading=9.5,
        textColor=c_code
    ))

    styles.add(ParagraphStyle(
        name='CalloutTitle',
        fontName='Helvetica-Bold',
        fontSize=9.5,
        leading=12.5
    ))

    styles.add(ParagraphStyle(
        name='CalloutBody',
        fontName='Helvetica',
        fontSize=8.5,
        leading=11.5,
        textColor=c_slate
    ))

    styles.add(ParagraphStyle(
        name='FigureCaption',
        fontName='Helvetica-Oblique',
        fontSize=8.5,
        leading=11.5,
        textColor=c_muted,
        alignment=1, # Center
        spaceBefore=4,
        spaceAfter=10
    ))

    styles.add(ParagraphStyle(
        name='TocItem',
        fontName='Helvetica-Bold',
        fontSize=9.5,
        leading=14,
        textColor=c_navy
    ))

    styles.add(ParagraphStyle(
        name='TocDots',
        fontName='Helvetica',
        fontSize=8.5,
        leading=14,
        textColor=c_muted
    ))

    return styles

# ----------------------------------------------------------------------
# CALLOUT & WIDGET BUILDERS
# ----------------------------------------------------------------------
def make_callout(title, body_text, kind="analogy", styles=None):
    """
    Creates an elegant Oxford-style Callout Box.
    kind: 'analogy' (amber/gold), 'deep_dive' (navy/blue), 'code_blueprint' (slate),
          'warning' (crimson), 'minilab' (emerald)
    """
    kind_configs = {
        "analogy": {
            "border": colors.HexColor("#D97706"),
            "bg": colors.HexColor("#FFFBEB"),
            "title_col": colors.HexColor("#92400E"),
            "icon": "💡 KID-FRIENDLY ANALOGY: THINK OF IT LIKE THIS!"
        },
        "deep_dive": {
            "border": colors.HexColor("#0284C7"),
            "bg": colors.HexColor("#EFF6FF"),
            "title_col": colors.HexColor("#002147"),
            "icon": "🔍 OXFORD SCHOLAR'S ARCHITECTURAL DEEP DIVE"
        },
        "code_blueprint": {
            "border": colors.HexColor("#64748B"),
            "bg": colors.HexColor("#F8FAFC"),
            "title_col": colors.HexColor("#1E293B"),
            "icon": "⚙️ CODE BLUEPRINT & IMPLEMENTATION DETAILS"
        },
        "warning": {
            "border": colors.HexColor("#DC2626"),
            "bg": colors.HexColor("#FEF2F2"),
            "title_col": colors.HexColor("#991B1B"),
            "icon": "⚠️ ARCHITECTURAL PITFALL & REGRESSION WARNING"
        },
        "minilab": {
            "border": colors.HexColor("#059669"),
            "bg": colors.HexColor("#ECFDF5"),
            "title_col": colors.HexColor("#065F46"),
            "icon": "🧪 TRY IT YOURSELF: THE 5-MINUTE ENGINEERING LAB"
        }
    }
    cfg = kind_configs.get(kind, kind_configs["deep_dive"])
    
    t_style = ParagraphStyle(
        'CTitle',
        parent=styles['CalloutTitle'],
        textColor=cfg["title_col"]
    )
    b_style = styles['CalloutBody']

    header_para = Paragraph(f"<b>{cfg['icon']}</b> — {title}", t_style)
    body_para = Paragraph(body_text, b_style)

    table_data = [[header_para], [body_para]]
    t = Table(table_data, colWidths=[490])
    t.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), cfg["bg"]),
        ('BOX', (0,0), (-1,-1), 1.5, cfg["border"]),
        ('TOPPADDING', (0,0), (-1,-1), 6),
        ('BOTTOMPADDING', (0,0), (-1,-1), 6),
        ('LEFTPADDING', (0,0), (-1,-1), 10),
        ('RIGHTPADDING', (0,0), (-1,-1), 10),
    ]))
    return t

def make_code_box(code_string, language="C# / .NET 8", styles=None):
    """
    Renders formatted syntax box with language badge.
    """
    lines = code_string.strip().split("\n")
    # Escape XML entities
    escaped_lines = []
    for l in lines:
        el = l.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
        escaped_lines.append(el)
    
    formatted_code = "<br/>".join(escaped_lines)
    p_code = Paragraph(f"<font color='#002147'><b>// [{language}]</b></font><br/>{formatted_code}", styles['CodeStyle'])

    t = Table([[p_code]], colWidths=[490])
    t.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), colors.HexColor("#F8FAFC")),
        ('BOX', (0,0), (-1,-1), 1, colors.HexColor("#CBD5E1")),
        ('LINELEFT', (0,0), (0,0), 3, colors.HexColor("#0284C7")),
        ('TOPPADDING', (0,0), (-1,-1), 5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 5),
        ('LEFTPADDING', (0,0), (-1,-1), 8),
        ('RIGHTPADDING', (0,0), (-1,-1), 8),
    ]))
    return t

def make_figure(image_filename, caption_text, styles=None, width=470, height=225):
    img_path = os.path.join(FIGURES_DIR, image_filename)
    if not os.path.exists(img_path):
        return Paragraph(f"<i>[Missing Figure: {image_filename}]</i>", styles['FigureCaption'])
    
    img = Image(img_path, width=width, height=height)
    cap = Paragraph(f"<b>Figure:</b> {caption_text}", styles['FigureCaption'])
    return KeepTogether([img, Spacer(1, 2), cap, Spacer(1, 4)])

print("Helper builders defined.")
