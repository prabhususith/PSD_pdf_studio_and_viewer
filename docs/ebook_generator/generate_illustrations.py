import os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as patches
import numpy as np

OUTPUT_DIR = os.path.join("docs", "ebook_generator", "figures")
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Common styling palette - Oxford Publication Theme
OXFORD_BLUE = "#002147"
OXFORD_NAVY = "#0A192F"
CRIMSON = "#8A1538"
CYAN_ACCENT = "#0284C7"
EMERALD_GREEN = "#059669"
AMBER_GOLD = "#D97706"
CARD_BG = "#F8FAFC"
BORDER_COLOR = "#CBD5E1"
TEXT_DARK = "#1E293B"

plt.rcParams['font.family'] = 'DejaVu Sans'
plt.rcParams['figure.autolayout'] = True

# ----------------------------------------------------------------------
# FIGURE 1: Master System Architecture Topology
# ----------------------------------------------------------------------
def make_fig1():
    fig, ax = plt.subplots(figsize=(9, 5), dpi=300)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 6)
    ax.axis('off')

    # Title
    ax.text(5, 5.6, "PSD PDF Studio: Enterprise High-Performance Topology", 
            ha='center', va='center', fontsize=14, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5, 5.25, "Dual-Platform Native Core: Windows x64 (.NET 8 WPF) & Android (API 23–35 ART)",
            ha='center', va='center', fontsize=9.5, style='italic', color="#475569")

    # Layer 1: Presentation Tier
    p1 = patches.FancyBboxPatch((0.5, 3.8), 4.2, 1.1, boxstyle="round,pad=0.1", fc="#EFF6FF", ec=CYAN_ACCENT, lw=2)
    ax.add_patch(p1)
    ax.text(2.6, 4.55, "Windows Presentation Tier (WPF)", ha='center', va='center', fontsize=11, fontweight='bold', color=OXFORD_BLUE)
    ax.text(2.6, 4.15, "105 Themes • Dual-Pane Inspector • Vector Viewport", ha='center', va='center', fontsize=8.5, color=TEXT_DARK)

    p2 = patches.FancyBboxPatch((5.3, 3.8), 4.2, 1.1, boxstyle="round,pad=0.1", fc="#F0FDF4", ec=EMERALD_GREEN, lw=2)
    ax.add_patch(p2)
    ax.text(7.4, 4.55, "Android Mobile Tier (Java / Dalvik)", ha='center', va='center', fontsize=11, fontweight='bold', color="#065F46")
    ax.text(7.4, 4.15, "Pinch-Zoom Canvas • Stylus Inking • Camera OCR", ha='center', va='center', fontsize=8.5, color=TEXT_DARK)

    # Layer 2: Core Engine Tier
    p3 = patches.FancyBboxPatch((0.5, 1.9), 4.2, 1.4, boxstyle="round,pad=0.1", fc="#FFFBEB", ec=AMBER_GOLD, lw=2)
    ax.add_patch(p3)
    ax.text(2.6, 2.95, "Vector Rasterizer & Memory Core", ha='center', va='center', fontsize=11, fontweight='bold', color="#92400E")
    ax.text(2.6, 2.55, "• 256MB LRU Tile Bitmap Cache\n• Double-Checked Eviction Guard\n• WinRT / DirectX Hardware Acceleration", ha='center', va='center', fontsize=8.5, color=TEXT_DARK)

    p4 = patches.FancyBboxPatch((5.3, 1.9), 4.2, 1.4, boxstyle="round,pad=0.1", fc="#FEF2F2", ec=CRIMSON, lw=2)
    ax.add_patch(p4)
    ax.text(7.4, 2.95, "Offline AI Copilot & Cryptography", ha='center', va='center', fontsize=11, fontweight='bold', color=CRIMSON)
    ax.text(7.4, 2.55, "• 100% Offline Heuristic Summarizer\n• SHA-256 / PKCS#7 Digital Signatures\n• ReDoS-Immune Token Extraction", ha='center', va='center', fontsize=8.5, color=TEXT_DARK)

    # Layer 3: Persistence Tier
    p5 = patches.FancyBboxPatch((0.5, 0.3), 9.0, 1.1, boxstyle="round,pad=0.1", fc="#F8FAFC", ec=OXFORD_BLUE, lw=2)
    ax.add_patch(p5)
    ax.text(5.0, 1.05, "Universal Storage & Release Artifact Pipeline", ha='center', va='center', fontsize=11, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5.0, 0.65, "Standalone Exe • Portable ZIP • Inno Setup • Enterprise MSI • Android Universal APK (v1/v2/v3 Signed)", ha='center', va='center', fontsize=8.5, color="#334155")

    # Arrows
    arrow_props = dict(arrowstyle="->", lw=2, color="#64748B", shrinkA=4, shrinkB=4)
    ax.annotate("", xy=(2.6, 3.3), xytext=(2.6, 3.8), arrowprops=arrow_props)
    ax.annotate("", xy=(7.4, 3.3), xytext=(7.4, 3.8), arrowprops=arrow_props)
    ax.annotate("", xy=(2.6, 1.4), xytext=(2.6, 1.9), arrowprops=arrow_props)
    ax.annotate("", xy=(7.4, 1.4), xytext=(7.4, 1.9), arrowprops=arrow_props)
    ax.annotate("", xy=(5.3, 2.6), xytext=(4.7, 2.6), arrowprops=dict(arrowstyle="<->", lw=1.5, color="#64748B"))

    plt.savefig(os.path.join(OUTPUT_DIR, "fig1_system_architecture.png"), bbox_inches='tight')
    plt.close()
    print("Fig 1 generated.")

# ----------------------------------------------------------------------
# FIGURE 2: The Document Abstract Syntax Tree (AST)
# ----------------------------------------------------------------------
def make_fig2():
    fig, ax = plt.subplots(figsize=(9, 4.5), dpi=300)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 5)
    ax.axis('off')

    ax.text(5, 4.65, "The Anatomy of a Document AST (Abstract Syntax Tree)", 
            ha='center', va='center', fontsize=13, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5, 4.35, "How PSD PDF Studio Represents Documents in Clean Object Memory", 
            ha='center', va='center', fontsize=9, style='italic', color="#475569")

    # Root Node
    root = patches.FancyBboxPatch((3.5, 3.2), 3.0, 0.8, boxstyle="round,pad=0.08", fc="#DBEAFE", ec=CYAN_ACCENT, lw=2)
    ax.add_patch(root)
    ax.text(5.0, 3.7, "PdfDocumentModel", ha='center', va='center', fontsize=10.5, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5.0, 3.4, "Title • FilePath • ZoomLevel • IsModified", ha='center', va='center', fontsize=7.5, color=TEXT_DARK)

    # Page Nodes
    pages = [
        (0.6, 1.8, "Page 1: Title & Cover", "612x792 pt • Rotation 0°"),
        (3.8, 1.8, "Page 2: Executive Text", "612x792 pt • Rotation 0°"),
        (7.0, 1.8, "Page 3: Engineering Spec", "612x792 pt • Rotation 90°")
    ]
    for x, y, title, sub in pages:
        p = patches.FancyBboxPatch((x, y), 2.4, 0.9, boxstyle="round,pad=0.08", fc="#FEF3C7", ec=AMBER_GOLD, lw=1.5)
        ax.add_patch(p)
        ax.text(x + 1.2, y + 0.6, title, ha='center', va='center', fontsize=9, fontweight='bold', color="#92400E")
        ax.text(x + 1.2, y + 0.3, sub, ha='center', va='center', fontsize=7.5, color=TEXT_DARK)
        # Connect to root
        ax.annotate("", xy=(x + 1.2, y + 0.9), xytext=(5.0, 3.2),
                    arrowprops=dict(arrowstyle="->", lw=1.5, color="#94A3B8"))

    # Annotation Nodes under Page 3
    annots = [
        (5.5, 0.4, "Vector Ink Stroke\n(Pen Markup)", "#DC2626"),
        (7.8, 0.4, "PKCS#7 Digital Seal\n(SHA-256 Signature)", "#059669")
    ]
    for x, y, text, col in annots:
        a = patches.FancyBboxPatch((x, y), 1.9, 0.7, boxstyle="round,pad=0.06", fc="#F8FAFC", ec=col, lw=1.5)
        ax.add_patch(a)
        ax.text(x + 0.95, y + 0.35, text, ha='center', va='center', fontsize=7.5, fontweight='bold', color=col)
        ax.annotate("", xy=(x + 0.95, y + 0.7), xytext=(8.2, 1.8),
                    arrowprops=dict(arrowstyle="->", lw=1.2, color="#94A3B8"))

    plt.savefig(os.path.join(OUTPUT_DIR, "fig2_ast_document_tree.png"), bbox_inches='tight')
    plt.close()
    print("Fig 2 generated.")

# ----------------------------------------------------------------------
# FIGURE 3: LRU Cache Eviction & Conveyor Belt
# ----------------------------------------------------------------------
def make_fig3():
    fig, ax = plt.subplots(figsize=(9, 4.5), dpi=300)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 5)
    ax.axis('off')

    ax.text(5, 4.65, "The Zero-Lag Conveyor Belt: LRU Cache Eviction Architecture", 
            ha='center', va='center', fontsize=13, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5, 4.35, "How 256MB LRU Double-Checked Capacity Bounds Guarantee 60–120 FPS", 
            ha='center', va='center', fontsize=9, style='italic', color="#475569")

    # Step 1: Render Request
    s1 = patches.FancyBboxPatch((0.4, 2.3), 1.8, 1.4, boxstyle="round,pad=0.08", fc="#E0F2FE", ec=CYAN_ACCENT, lw=2)
    ax.add_patch(s1)
    ax.text(1.3, 3.3, "1. Request", ha='center', va='center', fontsize=9.5, fontweight='bold', color=OXFORD_BLUE)
    ax.text(1.3, 2.75, "Render page X\nat 800px quantized\nwidth", ha='center', va='center', fontsize=8, color=TEXT_DARK)

    # Step 2: Cache Check
    s2 = patches.FancyBboxPatch((2.6, 2.3), 2.0, 1.4, boxstyle="round,pad=0.08", fc="#DCFCE7", ec=EMERALD_GREEN, lw=2)
    ax.add_patch(s2)
    ax.text(3.6, 3.3, "2. Cache Hit?", ha='center', va='center', fontsize=9.5, fontweight='bold', color="#065F46")
    ax.text(3.6, 2.75, "O(1) Concurrent\nDictionary lookup\nby HashKey", ha='center', va='center', fontsize=8, color=TEXT_DARK)

    # Step 3: WinRT Hardware Rasterizer
    s3 = patches.FancyBboxPatch((5.0, 2.3), 2.2, 1.4, boxstyle="round,pad=0.08", fc="#FEF3C7", ec=AMBER_GOLD, lw=2)
    ax.add_patch(s3)
    ax.text(6.1, 3.3, "3. GPU Rasterizer", ha='center', va='center', fontsize=9.5, fontweight='bold', color="#92400E")
    ax.text(6.1, 2.75, "RenderToStreamAsync\nFreeze() Bitmap\n(Zero UI Stall)", ha='center', va='center', fontsize=8, color=TEXT_DARK)

    # Step 4: Eviction Guard
    s4 = patches.FancyBboxPatch((7.6, 2.3), 2.0, 1.4, boxstyle="round,pad=0.08", fc="#FEE2E2", ec=CRIMSON, lw=2)
    ax.add_patch(s4)
    ax.text(8.6, 3.3, "4. LRU Eviction", ha='center', va='center', fontsize=9.5, fontweight='bold', color=CRIMSON)
    ax.text(8.6, 2.75, "If > 256MB:\nPurge oldest 30%\nunder lock", ha='center', va='center', fontsize=8, color=TEXT_DARK)

    # Connecting Arrows
    arrow_props = dict(arrowstyle="->", lw=2, color="#475569", shrinkA=3, shrinkB=3)
    ax.annotate("", xy=(2.6, 3.0), xytext=(2.2, 3.0), arrowprops=arrow_props)
    ax.annotate("", xy=(5.0, 3.0), xytext=(4.6, 3.0), arrowprops=arrow_props)
    ax.annotate("", xy=(7.6, 3.0), xytext=(7.2, 3.0), arrowprops=arrow_props)

    # Fast Return Loop
    ax.annotate("CACHE HIT (0.1ms)\nInstant Return!", xy=(1.3, 2.3), xytext=(3.6, 1.2),
                arrowprops=dict(arrowstyle="->", lw=2, color=EMERALD_GREEN, connectionstyle="arc3,rad=0.3"),
                ha='center', va='center', fontsize=8.5, fontweight='bold', color=EMERALD_GREEN)

    # Bottom summary box
    summary = patches.FancyBboxPatch((0.4, 0.25), 9.2, 0.65, boxstyle="round,pad=0.05", fc="#F1F5F9", ec="#94A3B8", lw=1)
    ax.add_patch(summary)
    ax.text(5.0, 0.57, "Formula: Memory per page (bytes) = Width x Height x 4 bytes (BGRA32). Eviction threshold: 256 MB.", 
            ha='center', va='center', fontsize=8, color="#334155")

    plt.savefig(os.path.join(OUTPUT_DIR, "fig3_magic_conveyor_lru.png"), bbox_inches='tight')
    plt.close()
    print("Fig 3 generated.")

# ----------------------------------------------------------------------
# FIGURE 4: Hand-drawn Style Analogy (The Magical Library & Reading Desk)
# ----------------------------------------------------------------------
def make_fig4():
    with plt.xkcd():
        fig, ax = plt.subplots(figsize=(9, 4.8), dpi=300)
        ax.set_xlim(0, 10)
        ax.set_ylim(0, 6)
        ax.axis('off')

        ax.text(5, 5.6, "The Magical Library Analogy: Why Computers Need RAM & Caches", 
                ha='center', va='center', fontsize=13, fontweight='bold', color=OXFORD_BLUE)

        # Big Bookshelf (Storage Disk - Slow, Holds 10,000 Books)
        shelf = patches.Rectangle((0.6, 1.2), 2.4, 3.8, fill=True, fc="#E2E8F0", ec="#334155", lw=2)
        ax.add_patch(shelf)
        ax.text(1.8, 4.6, "The Grand Bookshelf\n(Hard Drive / SSD)", ha='center', va='center', fontsize=9, fontweight='bold', color=OXFORD_BLUE)
        ax.text(1.8, 3.4, "Holds 10,000 books!\n\nTakes 10 seconds\nto walk here and\nfind a heavy book.", 
                ha='center', va='center', fontsize=8, color="#475569")

        # Small Reading Desk (RAM / LRU Cache - Fast, Holds 5 Books)
        desk = patches.Rectangle((4.2, 1.5), 3.0, 2.6, fill=True, fc="#FEF3C7", ec="#D97706", lw=2)
        ax.add_patch(desk)
        ax.text(5.7, 3.7, "The Student's Desk\n(256MB LRU Cache / RAM)", ha='center', va='center', fontsize=9.5, fontweight='bold', color="#92400E")
        ax.text(5.7, 2.7, "Super fast! Instant reading!\nOnly fits 5 books at once.\n\nWhen a 6th book arrives,\nthe least-read book goes\nback to the shelf!", 
                ha='center', va='center', fontsize=8, color="#78350F")

        # The Reader (CPU / GPU)
        reader = patches.Circle((8.7, 2.8), 0.9, fill=True, fc="#DBEAFE", ec=CYAN_ACCENT, lw=2)
        ax.add_patch(reader)
        ax.text(8.7, 3.0, "The Reader", ha='center', va='center', fontsize=10, fontweight='bold', color=OXFORD_BLUE)
        ax.text(8.7, 2.5, "(CPU / GPU)\nReads at\nLightning\nSpeed!", ha='center', va='center', fontsize=7.5, color="#1E40AF")

        # Connectors
        ax.annotate("Slow Trip (Disk I/O)", xy=(4.2, 2.8), xytext=(3.0, 2.8),
                    arrowprops=dict(arrowstyle="<->", lw=2, color="#64748B"), fontsize=8, ha='center')
        ax.annotate("Instant! (0.1ms)", xy=(7.8, 2.8), xytext=(7.2, 2.8),
                    arrowprops=dict(arrowstyle="<->", lw=2, color=EMERALD_GREEN), fontsize=8, ha='center', color=EMERALD_GREEN)

        ax.text(5, 0.5, "Takeaway for Kids: Keep the books you are reading on your desk, but clean up before it overflows!", 
                ha='center', va='center', fontsize=9, style='italic', color=CRIMSON)

        plt.savefig(os.path.join(OUTPUT_DIR, "fig4_kid_bookshop_analogy.png"), bbox_inches='tight')
        plt.close()
    print("Fig 4 generated.")

# ----------------------------------------------------------------------
# FIGURE 5: Android Page Navigation & Bitmap Lifecycle
# ----------------------------------------------------------------------
def make_fig5():
    fig, ax = plt.subplots(figsize=(9, 4.5), dpi=300)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 5)
    ax.axis('off')

    ax.text(5, 4.65, "Android ART Memory Management: The Bulletproof Bitmap Lifecycle", 
            ha='center', va='center', fontsize=13, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5, 4.35, "How Explicit recycle() Protects Budget Devices (Redmi Note 4 / Snapdragon 625)", 
            ha='center', va='center', fontsize=9, style='italic', color="#475569")

    # Flawed Pattern (Left)
    b_bad = patches.FancyBboxPatch((0.5, 1.2), 4.2, 2.8, boxstyle="round,pad=0.1", fc="#FEF2F2", ec=CRIMSON, lw=2)
    ax.add_patch(b_bad)
    ax.text(2.6, 3.65, "WITHOUT recycle() (The Crash Path)", ha='center', va='center', fontsize=10, fontweight='bold', color=CRIMSON)
    ax.text(2.6, 2.5, "1. User swipes to Page 2\n2. Page 1 Bitmap stays in Dalvik heap\n3. Rapid flipping allocates 33 MB/s\n4. GC triggers 'Stop-the-World' pause\n5. OutOfMemoryError -> CRASH!", 
            ha='center', va='center', fontsize=8.5, color="#7F1D1D")

    # Hardened Pattern (Right)
    b_good = patches.FancyBboxPatch((5.3, 1.2), 4.2, 2.8, boxstyle="round,pad=0.1", fc="#F0FDF4", ec=EMERALD_GREEN, lw=2)
    ax.add_patch(b_good)
    ax.text(7.4, 3.65, "WITH Explicit recycle() (LTS Hardened)", ha='center', va='center', fontsize=10, fontweight='bold', color="#065F46")
    ax.text(7.4, 2.5, "1. Check: if (mBitmap != null && !isRecycled())\n2. Instantly call: mBitmap.recycle()\n3. Physical raster memory returned to OS\n4. Zero ART GC stutter\n5. Fluid 60 FPS on 2GB RAM phones!", 
            ha='center', va='center', fontsize=8.5, color="#14532D")

    # Bottom metric badge
    badge = patches.FancyBboxPatch((1.5, 0.3), 7.0, 0.6, boxstyle="round,pad=0.05", fc="#EFF6FF", ec=CYAN_ACCENT, lw=1.5)
    ax.add_patch(badge)
    ax.text(5.0, 0.6, "Verified Benchmark: Peak Dalvik Heap Churn Dropped from 98 MB down to 12 MB (-87.7%)", 
            ha='center', va='center', fontsize=8.5, fontweight='bold', color=OXFORD_BLUE)

    plt.savefig(os.path.join(OUTPUT_DIR, "fig5_android_page_lifecycle.png"), bbox_inches='tight')
    plt.close()
    print("Fig 5 generated.")

# ----------------------------------------------------------------------
# FIGURE 6: Cryptographic Seal & PKCS#7 Digital Stamping
# ----------------------------------------------------------------------
def make_fig6():
    fig, ax = plt.subplots(figsize=(9, 4.5), dpi=300)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 5)
    ax.axis('off')

    ax.text(5, 4.65, "The Royal Wax Seal: Cryptographic Integrity & PKCS#7 Signatures", 
            ha='center', va='center', fontsize=13, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5, 4.35, "How Mathematics Guarantees Nobody Altered a Single Letter in Your Contract", 
            ha='center', va='center', fontsize=9, style='italic', color="#475569")

    # Step 1: Document
    s1 = patches.FancyBboxPatch((0.4, 1.8), 2.0, 2.0, boxstyle="round,pad=0.08", fc="#F8FAFC", ec=OXFORD_BLUE, lw=1.5)
    ax.add_patch(s1)
    ax.text(1.4, 3.4, "Contract.pdf", ha='center', va='center', fontsize=10, fontweight='bold', color=OXFORD_BLUE)
    ax.text(1.4, 2.5, "100 Pages\nof legal terms\nand agreements", ha='center', va='center', fontsize=8, color=TEXT_DARK)

    # Step 2: SHA-256 Hash
    s2 = patches.FancyBboxPatch((2.8, 1.8), 2.2, 2.0, boxstyle="round,pad=0.08", fc="#FEF3C7", ec=AMBER_GOLD, lw=1.5)
    ax.add_patch(s2)
    ax.text(3.9, 3.4, "SHA-256 Hasher", ha='center', va='center', fontsize=10, fontweight='bold', color="#92400E")
    ax.text(3.9, 2.5, "Condenses 100 pages\ninto a unique 64-char\nFingerprint:\n'9f8a8b7c...'", ha='center', va='center', fontsize=7.5, color=TEXT_DARK)

    # Step 3: Private Key Encryption (The Royal Seal)
    s3 = patches.FancyBboxPatch((5.4, 1.8), 2.2, 2.0, boxstyle="round,pad=0.08", fc="#FEF2F2", ec=CRIMSON, lw=1.5)
    ax.add_patch(s3)
    ax.text(6.5, 3.4, "Private Key Seal", ha='center', va='center', fontsize=10, fontweight='bold', color=CRIMSON)
    ax.text(6.5, 2.5, "Encrypts the fingerprint\nwith author's private key.\nOnly the author could\nhave stamped this!", ha='center', va='center', fontsize=7.5, color=TEXT_DARK)

    # Step 4: Verification
    s4 = patches.FancyBboxPatch((8.0, 1.8), 1.6, 2.0, boxstyle="round,pad=0.08", fc="#DCFCE7", ec=EMERALD_GREEN, lw=1.5)
    ax.add_patch(s4)
    ax.text(8.8, 3.4, "Verified!", ha='center', va='center', fontsize=10, fontweight='bold', color="#065F46")
    ax.text(8.8, 2.5, "Recipient checks\nwith Public Key.\n100% Authentic!", ha='center', va='center', fontsize=7.5, color=TEXT_DARK)

    # Arrows
    arr = dict(arrowstyle="->", lw=2, color="#64748B", shrinkA=3, shrinkB=3)
    ax.annotate("", xy=(2.8, 2.8), xytext=(2.4, 2.8), arrowprops=arr)
    ax.annotate("", xy=(5.4, 2.8), xytext=(5.0, 2.8), arrowprops=arr)
    ax.annotate("", xy=(8.0, 2.8), xytext=(7.6, 2.8), arrowprops=arr)

    # Kid Analogy Callout
    kid_box = patches.FancyBboxPatch((0.4, 0.4), 9.2, 0.9, boxstyle="round,pad=0.08", fc="#EFF6FF", ec=CYAN_ACCENT, lw=1)
    ax.add_patch(kid_box)
    ax.text(5.0, 0.85, "Kid Metaphor: It's like sealing a letter with the King's personal signet ring pressed into hot wax.", 
            ha='center', va='center', fontsize=8.5, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5.0, 0.6, "If someone opens it and changes even one letter, the wax seal breaks and everyone knows it was tampered with!", 
            ha='center', va='center', fontsize=8, color="#334155")

    plt.savefig(os.path.join(OUTPUT_DIR, "fig6_crypto_seal_diagram.png"), bbox_inches='tight')
    plt.close()
    print("Fig 6 generated.")

# ----------------------------------------------------------------------
# FIGURE 7: 100% Offline AI Copilot Intelligence Engine
# ----------------------------------------------------------------------
def make_fig7():
    fig, ax = plt.subplots(figsize=(9, 4.5), dpi=300)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 5)
    ax.axis('off')

    ax.text(5, 4.65, "100% Offline AI Intelligence Engine & Neural Heuristics", 
            ha='center', va='center', fontsize=13, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5, 4.35, "Zero Cloud Dependency • Complete Data Privacy • Sub-Millisecond Analysis", 
            ha='center', va='center', fontsize=9, style='italic', color="#475569")

    # Stages
    stages = [
        (0.4, "Raw Text Ingestion", "PDF / Word stream\nup to 100,000 words", "#EFF6FF", CYAN_ACCENT),
        (2.8, "Sentence Tokenizer", "ReDoS-immune regex\nbounded split pipeline", "#FEF3C7", AMBER_GOLD),
        (5.2, "Neural Heuristics", "• Executive Synthesis\n• Action Item Extraction\n• Risk Severity Scoring", "#FEF2F2", CRIMSON),
        (7.6, "Intelligence Exports", "• Plain English Translation\n• Financial Entity Matrix\n• Vector RAG JSON Chunks", "#DCFCE7", EMERALD_GREEN)
    ]
    for x, title, desc, fc, ec in stages:
        box = patches.FancyBboxPatch((x, 1.6), 2.0, 2.3, boxstyle="round,pad=0.08", fc=fc, ec=ec, lw=1.8)
        ax.add_patch(box)
        ax.text(x + 1.0, 3.5, title, ha='center', va='center', fontsize=9, fontweight='bold', color=OXFORD_BLUE)
        ax.text(x + 1.0, 2.5, desc, ha='center', va='center', fontsize=7.5, color=TEXT_DARK)

    # Connecting Arrows
    arr = dict(arrowstyle="->", lw=2, color="#64748B", shrinkA=3, shrinkB=3)
    ax.annotate("", xy=(2.8, 2.7), xytext=(2.4, 2.7), arrowprops=arr)
    ax.annotate("", xy=(5.2, 2.7), xytext=(4.8, 2.7), arrowprops=arr)
    ax.annotate("", xy=(7.6, 2.7), xytext=(7.2, 2.7), arrowprops=arr)

    # Privacy Guarantee
    p_box = patches.FancyBboxPatch((0.4, 0.3), 9.2, 0.9, boxstyle="round,pad=0.08", fc="#F8FAFC", ec=OXFORD_BLUE, lw=1.5)
    ax.add_patch(p_box)
    ax.text(5.0, 0.85, "PRIVACY SOVEREIGNTY: No document text, financial figure, or signature ever leaves your computer.", 
            ha='center', va='center', fontsize=8.5, fontweight='bold', color=OXFORD_BLUE)
    ax.text(5.0, 0.55, "Runs on airplanes, submarine vaults, and remote disaster zones without internet connection.", 
            ha='center', va='center', fontsize=8, color="#475569")

    plt.savefig(os.path.join(OUTPUT_DIR, "fig7_ai_copilot_pipeline.png"), bbox_inches='tight')
    plt.close()
    print("Fig 7 generated.")

# ----------------------------------------------------------------------
# FIGURE 8: Master Cheatsheet Infographic
# ----------------------------------------------------------------------
def make_fig8():
    fig, ax = plt.subplots(figsize=(9, 5.5), dpi=300)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 6)
    ax.axis('off')

    ax.text(5, 5.65, "The Master Software Engineering Codex: 8 Roles At A Glance", 
            ha='center', va='center', fontsize=14, fontweight='bold', color=OXFORD_BLUE)

    roles = [
        ("1. Chief Architect", "AST Document Tree, O(1) structures, coordinate matrix", CYAN_ACCENT, 0.4, 3.8),
        ("2. Performance Lead", "256MB LRU Cache, zero-copy Spans, GC minimization", AMBER_GOLD, 5.1, 3.8),
        ("3. UI/UX Designer", "105 Themes, dual-pane inspector, WCAG contrast", "#8B5CF6", 0.4, 2.5),
        ("4. Android Specialist", "classes.dex, ART GC lifecycle, bitmap.recycle()", EMERALD_GREEN, 5.1, 2.5),
        ("5. Security Vault Guard", "SHA-256 fingerprint, PKCS#7 seal, ReDoS defense", CRIMSON, 0.4, 1.2),
        ("6. AI Copilot Oracle", "Offline heuristic NLP, Action items, RAG embeddings", "#EC4899", 5.1, 1.2)
    ]

    for title, desc, col, x, y in roles:
        box = patches.FancyBboxPatch((x, y), 4.5, 1.05, boxstyle="round,pad=0.08", fc="#F8FAFC", ec=col, lw=2)
        ax.add_patch(box)
        ax.text(x + 0.2, y + 0.75, title, ha='left', va='center', fontsize=9.5, fontweight='bold', color=col)
        ax.text(x + 0.2, y + 0.35, desc, ha='left', va='center', fontsize=8, color=TEXT_DARK)

    # Footer banner
    footer = patches.FancyBboxPatch((0.4, 0.15), 9.2, 0.8, boxstyle="round,pad=0.08", fc=OXFORD_BLUE, ec=AMBER_GOLD, lw=1.5)
    ax.add_patch(footer)
    ax.text(5.0, 0.62, "THE GOLDEN FORMULA OF LEGENDARY SOFTWARE", ha='center', va='center', fontsize=9.5, fontweight='bold', color="#F8FAFC")
    ax.text(5.0, 0.35, "Simplicity in Conception • Mathematical Rigor in Execution • Zero Compromise in User Experience", 
            ha='center', va='center', fontsize=8, color="#94A3B8")

    plt.savefig(os.path.join(OUTPUT_DIR, "fig8_master_cheatsheet.png"), bbox_inches='tight')
    plt.close()
    print("Fig 8 generated.")

if __name__ == "__main__":
    make_fig1()
    make_fig2()
    make_fig3()
    make_fig4()
    make_fig5()
    make_fig6()
    make_fig7()
    make_fig8()
    print("All 8 figures successfully generated in", OUTPUT_DIR)
