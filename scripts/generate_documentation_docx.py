"""
Enterprise Master DOCX Documentation Generator
Project: Multi-Tenant Microservices Platform for a Healthcare Payer Value Chain on Microsoft Azure
Generates: docs/final-delivery/Healthcare-MultiTenant-Platform-Documentation.docx
From: docs/final-delivery/Healthcare-MultiTenant-Platform-Documentation.md
"""

import os
import re
import sys
import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn

# Color Palette Constants
HEX_PRIMARY = "1B365D"       # Deep Navy
HEX_SECONDARY = "0F766E"     # Deep Teal
HEX_DARK = "1E293B"          # Slate Dark
HEX_MUTED = "64748B"         # Slate Muted
HEX_BORDER = "CBD5E1"        # Light Border
HEX_BG_LIGHT = "F8FAFC"      # Off-white / light slate
HEX_ALERT_BG = "FEF2F2"      # Alert light red
HEX_ALERT_BORDER = "EF4444"  # Alert border red
HEX_CODE_BG = "F1F5F9"       # Code background

COLOR_PRIMARY = RGBColor(27, 54, 93)
COLOR_SECONDARY = RGBColor(15, 118, 110)
COLOR_DARK = RGBColor(30, 41, 59)
COLOR_MUTED = RGBColor(100, 116, 139)

def set_cell_background(cell, hex_color):
    """Sets background color of a docx table cell."""
    tcPr = cell._element.get_or_add_tcPr()
    shd = parse_xml(f'<w:shd {nsdecls("w")} w:fill="{hex_color}"/>')
    tcPr.append(shd)

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
    """Sets internal padding (in twips) of a docx table cell."""
    tcPr = cell._element.get_or_add_tcPr()
    tcMar = parse_xml(
        f'<w:tcMar {nsdecls("w")}>'
        f'<w:top w:w="{top}" w:type="dxa"/>'
        f'<w:bottom w:w="{bottom}" w:type="dxa"/>'
        f'<w:left w:w="{left}" w:type="dxa"/>'
        f'<w:right w:w="{right}" w:type="dxa"/>'
        f'</w:tcMar>'
    )
    tcPr.append(tcMar)

def set_table_borders(table, hex_color=HEX_BORDER):
    """Sets thin subtle borders on a docx table."""
    tblPr = table._element.xpath('w:tblPr')
    if tblPr:
        borders = parse_xml(
            f'<w:tblBorders {nsdecls("w")}>'
            f'<w:top w:val="single" w:sz="4" w:space="0" w:color="{hex_color}"/>'
            f'<w:bottom w:val="single" w:sz="4" w:space="0" w:color="{hex_color}"/>'
            f'<w:left w:val="single" w:sz="4" w:space="0" w:color="{hex_color}"/>'
            f'<w:right w:val="single" w:sz="4" w:space="0" w:color="{hex_color}"/>'
            f'<w:insideH w:val="single" w:sz="4" w:space="0" w:color="{hex_color}"/>'
            f'<w:insideV w:val="single" w:sz="4" w:space="0" w:color="{hex_color}"/>'
            f'</w:tblBorders>'
        )
        tblPr[0].append(borders)

def add_formatted_text(paragraph, text, default_color=COLOR_DARK, font_size=10, default_font="Calibri"):
    """Parses markdown inline bold, code, and links, appending formatted runs."""
    # Pattern to match **bold**, `code`, or regular text
    tokens = re.split(r'(\*\*.*?\*\*|`.*?`|\[.*?\]\(.*?\))', text)
    for token in tokens:
        if not token:
            continue
        if token.startswith('**') and token.endswith('**'):
            bold_text = token[2:-2]
            run = paragraph.add_run(bold_text)
            run.bold = True
            run.font.name = default_font
            run.font.size = Pt(font_size)
            run.font.color.rgb = default_color
        elif token.startswith('`') and token.endswith('`'):
            code_text = token[1:-1]
            run = paragraph.add_run(code_text)
            run.font.name = "Consolas"
            run.font.size = Pt(font_size - 1)
            run.font.color.rgb = RGBColor(15, 23, 42)
        elif token.startswith('[') and '](' in token and token.endswith(')'):
            match = re.match(r'\[(.*?)\]\((.*?)\)', token)
            if match:
                link_text, url = match.groups()
                run = paragraph.add_run(link_text)
                run.font.name = default_font
                run.font.size = Pt(font_size)
                run.font.color.rgb = COLOR_SECONDARY
                run.underline = True
        else:
            run = paragraph.add_run(token)
            run.font.name = default_font
            run.font.size = Pt(font_size)
            run.font.color.rgb = default_color

def build_docx_from_markdown(md_path, docx_path):
    doc = docx.Document()

    # Configure 0.75-inch page margins (54 pt)
    sections = doc.sections
    for s in sections:
        s.top_margin = Inches(0.75)
        s.bottom_margin = Inches(0.75)
        s.left_margin = Inches(0.75)
        s.right_margin = Inches(0.75)

        # Header setup
        header = s.header
        hp = header.paragraphs[0]
        hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
        hrun = hp.add_run("HEALTHCARE MULTI-TENANT MICROSERVICES PLATFORM ON AZURE | Project Closure – Version 1.0")
        hrun.font.name = "Calibri"
        hrun.font.size = Pt(8.5)
        hrun.font.color.rgb = COLOR_MUTED

        # Footer setup
        footer = s.footer
        fp = footer.paragraphs[0]
        fp.alignment = WD_ALIGN_PARAGRAPH.LEFT
        frun1 = fp.add_run("CONFIDENTIAL & PROPRIETARY | Enterprise Architecture Archive")
        frun1.font.name = "Calibri"
        frun1.font.size = Pt(8.5)
        frun1.font.color.rgb = RGBColor(153, 27, 27)
        frun1.bold = True

    with open(md_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    in_code_block = False
    code_lines = []
    code_lang = ""
    in_table = False
    table_lines = []
    is_first_h2 = True

    def flush_table(tbl_lines):
        if not tbl_lines:
            return
        # Parse rows
        rows_data = []
        for line in tbl_lines:
            line_str = line.strip()
            if not line_str or line_str.startswith('| :---') or line_str.startswith('|:---') or line_str.startswith('|---'):
                continue
            cells = [c.strip() for c in line_str.split('|')[1:-1]]
            if cells:
                rows_data.append(cells)
        if not rows_data:
            return

        col_count = max(len(r) for r in rows_data)
        # Pad shorter rows
        for r in rows_data:
            while len(r) < col_count:
                r.append("")

        table = doc.add_table(rows=len(rows_data), cols=col_count)
        table.alignment = WD_TABLE_ALIGNMENT.CENTER
        set_table_borders(table, HEX_BORDER)

        for row_idx, row in enumerate(rows_data):
            for col_idx, cell_text in enumerate(row):
                cell = table.cell(row_idx, col_idx)
                cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
                set_cell_margins(cell, top=80, bottom=80, left=120, right=120)
                p = cell.paragraphs[0]
                p.paragraph_format.space_before = Pt(2)
                p.paragraph_format.space_after = Pt(2)
                p.paragraph_format.line_spacing = 1.05

                if row_idx == 0:
                    set_cell_background(cell, HEX_PRIMARY)
                    run = p.add_run(cell_text.replace('**', ''))
                    run.bold = True
                    run.font.name = "Calibri"
                    run.font.size = Pt(8.5)
                    run.font.color.rgb = RGBColor(255, 255, 255)
                else:
                    if row_idx % 2 == 1:
                        set_cell_background(cell, HEX_BG_LIGHT)
                    else:
                        set_cell_background(cell, "FFFFFF")
                    add_formatted_text(p, cell_text, default_color=COLOR_DARK, font_size=8.5)

        # Add a trailing space after table
        sp = doc.add_paragraph()
        sp.paragraph_format.space_before = Pt(0)
        sp.paragraph_format.space_after = Pt(4)

    def flush_code(c_lines, lang):
        if not c_lines:
            return
        code_text = "".join(c_lines)
        table = doc.add_table(rows=1, cols=1)
        table.alignment = WD_TABLE_ALIGNMENT.CENTER
        cell = table.cell(0, 0)
        set_cell_background(cell, HEX_CODE_BG)
        set_cell_margins(cell, top=100, bottom=100, left=150, right=150)
        set_table_borders(table, HEX_BORDER)

        p = cell.paragraphs[0]
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(0)
        p.paragraph_format.line_spacing = 1.0
        run = p.add_run(code_text.rstrip())
        run.font.name = "Consolas"
        run.font.size = Pt(7.8)
        run.font.color.rgb = RGBColor(15, 23, 42)

        sp = doc.add_paragraph()
        sp.paragraph_format.space_before = Pt(0)
        sp.paragraph_format.space_after = Pt(4)

    idx = 0
    while idx < len(lines):
        line = lines[idx]
        stripped = line.strip()

        # Handle Code Block delimiters
        if stripped.startswith('```'):
            if in_code_block:
                in_code_block = False
                flush_code(code_lines, code_lang)
                code_lines = []
                code_lang = ""
            else:
                if in_table:
                    in_table = False
                    flush_table(table_lines)
                    table_lines = []
                in_code_block = True
                code_lang = stripped[3:].strip()
            idx += 1
            continue

        if in_code_block:
            code_lines.append(line)
            idx += 1
            continue

        # Handle Table rows
        if stripped.startswith('|') and stripped.endswith('|'):
            in_table = True
            table_lines.append(line)
            idx += 1
            continue
        else:
            if in_table:
                in_table = False
                flush_table(table_lines)
                table_lines = []

        # Empty lines
        if not stripped:
            idx += 1
            continue

        # Blockquote / Advisory box
        if stripped.startswith('> '):
            quote_lines = [stripped[2:]]
            while idx + 1 < len(lines) and lines[idx + 1].strip().startswith('> '):
                idx += 1
                quote_lines.append(lines[idx].strip()[2:])
            quote_text = " ".join(quote_lines)

            table = doc.add_table(rows=1, cols=1)
            table.alignment = WD_TABLE_ALIGNMENT.CENTER
            cell = table.cell(0, 0)
            set_cell_background(cell, HEX_ALERT_BG)
            set_cell_margins(cell, top=120, bottom=120, left=180, right=180)

            # Red border
            tcPr = cell._element.get_or_add_tcPr()
            tcBorders = parse_xml(
                f'<w:tcBorders {nsdecls("w")}>'
                f'<w:top w:val="single" w:sz="6" w:space="0" w:color="{HEX_ALERT_BORDER}"/>'
                f'<w:bottom w:val="single" w:sz="6" w:space="0" w:color="{HEX_ALERT_BORDER}"/>'
                f'<w:left w:val="single" w:sz="18" w:space="0" w:color="{HEX_ALERT_BORDER}"/>'
                f'<w:right w:val="single" w:sz="6" w:space="0" w:color="{HEX_ALERT_BORDER}"/>'
                f'</w:tcBorders>'
            )
            tcPr.append(tcBorders)

            p = cell.paragraphs[0]
            p.paragraph_format.space_before = Pt(2)
            p.paragraph_format.space_after = Pt(2)
            p.paragraph_format.line_spacing = 1.15
            add_formatted_text(p, quote_text, default_color=RGBColor(127, 29, 29), font_size=9)
            idx += 1
            continue

        # Title (# )
        if stripped.startswith('# '):
            p = doc.add_paragraph()
            p.paragraph_format.space_before = Pt(12)
            p.paragraph_format.space_after = Pt(6)
            run = p.add_run(stripped[2:])
            run.bold = True
            run.font.name = "Arial"
            run.font.size = Pt(20)
            run.font.color.rgb = COLOR_PRIMARY
            idx += 1
            continue

        # Chapter Headings (## Chapter ...)
        if stripped.startswith('## '):
            heading_text = stripped[3:]
            if not is_first_h2:
                doc.add_page_break()
            else:
                is_first_h2 = False
            p = doc.add_paragraph()
            p.paragraph_format.space_before = Pt(8)
            p.paragraph_format.space_after = Pt(6)
            run = p.add_run(heading_text)
            run.bold = True
            run.font.name = "Arial"
            run.font.size = Pt(15)
            run.font.color.rgb = COLOR_PRIMARY
            idx += 1
            continue

        # Section Headings (### )
        if stripped.startswith('### '):
            p = doc.add_paragraph()
            p.paragraph_format.space_before = Pt(10)
            p.paragraph_format.space_after = Pt(4)
            run = p.add_run(stripped[4:])
            run.bold = True
            run.font.name = "Arial"
            run.font.size = Pt(12)
            run.font.color.rgb = COLOR_SECONDARY
            idx += 1
            continue

        # Subsection Headings (#### )
        if stripped.startswith('#### '):
            p = doc.add_paragraph()
            p.paragraph_format.space_before = Pt(8)
            p.paragraph_format.space_after = Pt(3)
            run = p.add_run(stripped[5:])
            run.bold = True
            run.font.name = "Arial"
            run.font.size = Pt(10.5)
            run.font.color.rgb = COLOR_DARK
            idx += 1
            continue

        # Horizontal Rule (---)
        if stripped == '---':
            p = doc.add_paragraph()
            p.paragraph_format.space_before = Pt(4)
            p.paragraph_format.space_after = Pt(4)
            r = p.add_run("_________________________________________________________________________________")
            r.font.name = "Calibri"
            r.font.size = Pt(8)
            r.font.color.rgb = RGBColor(203, 213, 225)
            idx += 1
            continue

        # Bullet list (* or -)
        if stripped.startswith('* ') or stripped.startswith('- '):
            p = doc.add_paragraph()
            p.paragraph_format.left_indent = Inches(0.25)
            p.paragraph_format.space_before = Pt(1)
            p.paragraph_format.space_after = Pt(3)
            p.paragraph_format.line_spacing = 1.15

            bullet_run = p.add_run("• ")
            bullet_run.bold = True
            bullet_run.font.name = "Calibri"
            bullet_run.font.size = Pt(9.5)
            bullet_run.font.color.rgb = COLOR_SECONDARY

            add_formatted_text(p, stripped[2:], default_color=COLOR_DARK, font_size=9.5)
            idx += 1
            continue

        # Standard Body Paragraph
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(2)
        p.paragraph_format.space_after = Pt(5)
        p.paragraph_format.line_spacing = 1.15
        add_formatted_text(p, stripped, default_color=COLOR_DARK, font_size=9.5)
        idx += 1

    # Final flush in case file ends with table
    if in_table:
        flush_table(table_lines)

    os.makedirs(os.path.dirname(docx_path), exist_ok=True)
    doc.save(docx_path)
    print(f"DOCX Generated Successfully: {docx_path} ({os.path.getsize(docx_path):,} bytes)")

if __name__ == '__main__':
    src = sys.argv[1] if len(sys.argv) > 1 else 'docs/final-delivery/Healthcare-MultiTenant-Platform-Documentation.md'
    dst = sys.argv[2] if len(sys.argv) > 2 else 'docs/final-delivery/Healthcare-MultiTenant-Platform-Documentation.docx'
    build_docx_from_markdown(src, dst)
