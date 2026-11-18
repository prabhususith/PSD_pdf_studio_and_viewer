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
# Oxford Two-Pass Numbered Canvas for Dynamic Page Numbering & Headers
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
        # Suppress running headers/footers on Cover Page (Page 1) and Half-Title (Page 2)
        if self._pageNumber <= 2:
            return

        self.saveState()
        self.setFont("Helvetica-Bold", 8)
        self.setFillColor(colors.HexColor("#002147")) # Oxford Navy

        # Running Header (Top)
        header_text = "PSD PDF STUDIO & VIEWER • ARCHITECTURAL & ENGINEERING TREATISE"
        self.drawString(54, 750, header_text)
        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.75)
        self.line(54, 744, 558, 744)

        # Running Footer (Bottom)
        self.setFont("Helvetica", 8)
        self.setFillColor(colors.HexColor("#64748B"))
        self.drawString(54, 38, "Oxford Software Engineering Guild • Enterprise Edition 4.0 LTS")
        
        page_str = f"Page {self._pageNumber} of {total_pages}"
        self.drawRightString(558, 38, page_str)
        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.75)
        self.line(54, 48, 558, 48)

        self.restoreState()

print("Canvas class defined successfully.")
