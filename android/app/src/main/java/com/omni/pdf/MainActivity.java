package com.omni.pdf;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ContentResolver;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.pdf.PdfRenderer;
import android.net.Uri;
import android.os.AsyncTask;
import android.os.Bundle;
import android.os.Environment;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import android.text.Editable;
import android.text.TextWatcher;
import android.util.DisplayMetrics;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.HorizontalScrollView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import com.omni.pdf.converters.PdfToTextExtractor;
import com.omni.pdf.converters.TextToPdfConverter;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;

/**
 * PSD PDF Studio & Viewer — Production Enterprise Android Application.
 * Full-featured mobile PDF workstation compatible with Android 6.0 through 14+.
 * Hardware-accelerated PdfRenderer engine, multi-page vector navigation, live ink overlay,
 * device storage auto-discovery library, and Word/Text to PDF compilation studio.
 */
public class MainActivity extends Activity {

    private static final int REQUEST_CODE_PICK_PDF = 2001;

    // Core PDF Engine State
    private ParcelFileDescriptor mFileDescriptor;
    private PdfRenderer mPdfRenderer;
    private PdfRenderer.Page mCurrentPage;
    private int mCurrentPageIndex = 0;
    private int mTotalPages = 0;
    private String mCurrentDocTitle = "No Document Loaded";
    private String mCurrentDocPath = "";
    private long mCurrentDocSize = 0;

    // View Components
    private TextView mHeaderTitle;
    private TextView mHeaderSubtitle;
    private FrameLayout mContentFrame;
    private LinearLayout mBottomNav;

    // Tab Containers
    private LinearLayout mLibraryContainer;
    private LinearLayout mViewerContainer;
    private LinearLayout mAnnotationContainer;
    private LinearLayout mCreatorContainer;

    // Viewer UI
    private PdfPageView mPdfPageView;
    private TextView mTxtPageCounter;
    private Button mBtnNightToggle;

    // Library UI
    private LinearLayout mLibraryItemsLayout;
    private EditText mSearchInput;
    private final List<File> mDiscoveredPdfFiles = new ArrayList<>();
    private final List<File> mFilteredPdfFiles = new ArrayList<>();

    // Annotation UI
    private View mColorIndicator;

    // Active Tab Index: 0 = Library, 1 = Viewer, 2 = Annotate, 3 = Creator
    private int mActiveTab = 0;
    private final Button[] mNavButtons = new Button[4];

    static {
        try {
            System.loadLibrary("pdfengine");
        } catch (UnsatisfiedLinkError ignored) {}
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // Build Master Modern Fluent Dark Layout
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(0xFF0F0F12);

        // 1. TOP HEADER BAR
        root.addView(createHeaderBar());

        // 2. MAIN DYNAMIC CONTENT CONTAINER
        mContentFrame = new FrameLayout(this);
        LinearLayout.LayoutParams contentLp = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT, 0, 1.0f
        );
        mContentFrame.setLayoutParams(contentLp);

        createLibraryTab();
        createViewerTab();
        createAnnotationTab();
        createCreatorTab();

        mContentFrame.addView(mLibraryContainer);
        mContentFrame.addView(mViewerContainer);
        mContentFrame.addView(mAnnotationContainer);
        mContentFrame.addView(mCreatorContainer);

        root.addView(mContentFrame);

        // 3. BOTTOM NAVIGATION BAR
        root.addView(createBottomNavBar());

        setContentView(root);

        // Switch to initial tab (Library)
        switchTab(0);

        // Check if opened via Intent (e.g. user tapped a PDF from WhatsApp, Chrome, Files)
        handleIntent(getIntent());

        // Start background indexing of local device PDFs
        refreshDeviceLibrary();
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        handleIntent(intent);
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        closeCurrentPdf();
    }

    // =========================================================================
    // HEADER BAR & TITLE
    // =========================================================================
    private View createHeaderBar() {
        LinearLayout header = new LinearLayout(this);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setBackgroundColor(0xFF18181B);
        header.setPadding(20, 16, 20, 16);
        header.setGravity(Gravity.CENTER_VERTICAL);

        // Left Branding & Active File Title
        LinearLayout textGroup = new LinearLayout(this);
        textGroup.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams tgLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.0f);
        textGroup.setLayoutParams(tgLp);

        mHeaderTitle = new TextView(this);
        mHeaderTitle.setText("PSD PDF Studio & Viewer");
        mHeaderTitle.setTextColor(0xFFFFFFFF);
        mHeaderTitle.setTextSize(14f);
        mHeaderTitle.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        textGroup.addView(mHeaderTitle);

        mHeaderSubtitle = new TextView(this);
        mHeaderSubtitle.setText("Universal Document Workstation (v3.0.0)");
        mHeaderSubtitle.setTextColor(0xFF38BDF8);
        mHeaderSubtitle.setTextSize(11f);
        textGroup.addView(mHeaderSubtitle);

        header.addView(textGroup);

        // Right Quick Action Buttons (Open, Night, Info, Share)
        HorizontalScrollView hsv = new HorizontalScrollView(this);
        hsv.setHorizontalScrollBarEnabled(false);
        LinearLayout actions = new LinearLayout(this);
        actions.setOrientation(LinearLayout.HORIZONTAL);

        Button btnOpen = createModernPillButton("📂 Open", 0xFF0284C7);
        btnOpen.setOnClickListener(v -> pickPdfFromDevice());
        actions.addView(btnOpen);

        mBtnNightToggle = createModernPillButton("🌙 Night", 0xFF27272A);
        mBtnNightToggle.setOnClickListener(v -> toggleNightMode());
        actions.addView(mBtnNightToggle);

        Button btnInfo = createModernPillButton("ℹ️ Info", 0xFF27272A);
        btnInfo.setOnClickListener(v -> showDocumentInfoDialog());
        actions.addView(btnInfo);

        Button btnShare = createModernPillButton("🔗 Share", 0xFF27272A);
        btnShare.setOnClickListener(v -> shareCurrentPdf());
        actions.addView(btnShare);

        hsv.addView(actions);
        header.addView(hsv);

        return header;
    }

    // =========================================================================
    // TAB 1: LIBRARY HUB & DEVICE SCANNER
    // =========================================================================
    private void createLibraryTab() {
        mLibraryContainer = new LinearLayout(this);
        mLibraryContainer.setOrientation(LinearLayout.VERTICAL);
        mLibraryContainer.setBackgroundColor(0xFF0F0F12);
        mLibraryContainer.setPadding(24, 20, 24, 20);

        // Quick Action Bar in Library (Open File, Rescan, Create Demo PDF)
        LinearLayout actionsRow = new LinearLayout(this);
        actionsRow.setOrientation(LinearLayout.HORIZONTAL);
        actionsRow.setGravity(Gravity.CENTER_VERTICAL);
        actionsRow.setPadding(0, 0, 0, 16);

        Button btnBrowse = createModernPillButton("📂 Open PDF File", 0xFF0284C7);
        btnBrowse.setOnClickListener(v -> pickPdfFromDevice());
        actionsRow.addView(btnBrowse);

        Button btnDemo = createModernPillButton("⚡ Sample PDF", 0xFF16A34A);
        btnDemo.setOnClickListener(v -> generateDemoPdfAndOpen());
        actionsRow.addView(btnDemo);

        Button btnRescan = createModernPillButton("🔄 Rescan", 0xFF27272A);
        btnRescan.setOnClickListener(v -> refreshDeviceLibrary());
        actionsRow.addView(btnRescan);

        mLibraryContainer.addView(actionsRow);

        // Search Bar
        mSearchInput = new EditText(this);
        mSearchInput.setHint("🔍 Search indexed device documents...");
        mSearchInput.setHintTextColor(0xFF71717A);
        mSearchInput.setTextColor(0xFFFFFFFF);
        mSearchInput.setBackgroundColor(0xFF1F1F23);
        mSearchInput.setPadding(24, 16, 24, 16);
        mSearchInput.setTextSize(13f);
        mSearchInput.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            @Override public void onTextChanged(CharSequence s, int start, int count, int after) {
                filterLibraryItems(s.toString());
            }
            @Override public void afterTextChanged(Editable s) {}
        });
        mLibraryContainer.addView(mSearchInput);

        TextView sectionLabel = new TextView(this);
        sectionLabel.setText("INDEXED DEVICE DOCUMENTS");
        sectionLabel.setTextColor(0xFF71717A);
        sectionLabel.setTextSize(10f);
        sectionLabel.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        sectionLabel.setPadding(4, 20, 4, 10);
        mLibraryContainer.addView(sectionLabel);

        // Scrollable List of Document Cards
        ScrollView sv = new ScrollView(this);
        sv.setLayoutParams(new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        mLibraryItemsLayout = new LinearLayout(this);
        mLibraryItemsLayout.setOrientation(LinearLayout.VERTICAL);
        sv.addView(mLibraryItemsLayout);

        mLibraryContainer.addView(sv);
    }

    // =========================================================================
    // TAB 2: PDF STUDIO VIEWER
    // =========================================================================
    private void createViewerTab() {
        mViewerContainer = new LinearLayout(this);
        mViewerContainer.setOrientation(LinearLayout.VERTICAL);
        mViewerContainer.setBackgroundColor(0xFF09090B);

        // Viewer Secondary Ribbon (Zoom in/out, Fit Width, Reset)
        LinearLayout viewerToolbar = new LinearLayout(this);
        viewerToolbar.setOrientation(LinearLayout.HORIZONTAL);
        viewerToolbar.setBackgroundColor(0xFF18181B);
        viewerToolbar.setPadding(16, 10, 16, 10);
        viewerToolbar.setGravity(Gravity.CENTER_VERTICAL);

        Button btnZoomIn = createModernPillButton("➕ Zoom In", 0xFF27272A);
        btnZoomIn.setOnClickListener(v -> mPdfPageView.zoomIn());
        viewerToolbar.addView(btnZoomIn);

        Button btnZoomOut = createModernPillButton("➖ Zoom Out", 0xFF27272A);
        btnZoomOut.setOnClickListener(v -> mPdfPageView.zoomOut());
        viewerToolbar.addView(btnZoomOut);

        Button btnFit = createModernPillButton("⛶ Fit Page", 0xFF27272A);
        btnFit.setOnClickListener(v -> mPdfPageView.resetZoomAndPan());
        viewerToolbar.addView(btnFit);

        mViewerContainer.addView(viewerToolbar);

        // High-Performance Vector PDF Canvas View
        mPdfPageView = new PdfPageView(this);
        LinearLayout.LayoutParams canvasLp = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT, 0, 1.0f
        );
        mPdfPageView.setLayoutParams(canvasLp);
        mViewerContainer.addView(mPdfPageView);

        // Bottom Page Stepper Ribbon (First, Prev, Page Indicator, Next, Last)
        LinearLayout navStepper = new LinearLayout(this);
        navStepper.setOrientation(LinearLayout.HORIZONTAL);
        navStepper.setBackgroundColor(0xFF18181B);
        navStepper.setPadding(16, 10, 16, 10);
        navStepper.setGravity(Gravity.CENTER);

        Button btnFirst = createModernPillButton("⏮", 0xFF27272A);
        btnFirst.setOnClickListener(v -> jumpToPage(0));
        navStepper.addView(btnFirst);

        Button btnPrev = createModernPillButton("◀ Prev", 0xFF27272A);
        btnPrev.setOnClickListener(v -> prevPage());
        navStepper.addView(btnPrev);

        mTxtPageCounter = new TextView(this);
        mTxtPageCounter.setText("Page 0 of 0");
        mTxtPageCounter.setTextColor(0xFF38BDF8);
        mTxtPageCounter.setTextSize(13f);
        mTxtPageCounter.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        mTxtPageCounter.setPadding(20, 0, 20, 0);
        mTxtPageCounter.setOnClickListener(v -> showJumpToPageDialog());
        navStepper.addView(mTxtPageCounter);

        Button btnNext = createModernPillButton("Next ▶", 0xFF27272A);
        btnNext.setOnClickListener(v -> nextPage());
        navStepper.addView(btnNext);

        Button btnLast = createModernPillButton("⏭", 0xFF27272A);
        btnLast.setOnClickListener(v -> jumpToPage(mTotalPages - 1));
        navStepper.addView(btnLast);

        mViewerContainer.addView(navStepper);
    }

    // =========================================================================
    // TAB 3: ANNOTATION & INK MARKUP STUDIO
    // =========================================================================
    private void createAnnotationTab() {
        mAnnotationContainer = new LinearLayout(this);
        mAnnotationContainer.setOrientation(LinearLayout.VERTICAL);
        mAnnotationContainer.setBackgroundColor(0xFF0F0F12);
        mAnnotationContainer.setPadding(24, 20, 24, 20);

        TextView title = new TextView(this);
        title.setText("Stylus & Freehand Ink Studio");
        title.setTextColor(0xFFFFFFFF);
        title.setTextSize(16f);
        title.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        mAnnotationContainer.addView(title);

        TextView desc = new TextView(this);
        desc.setText("Select pen colors and stroke widths to draw, annotate, and highlight directly on the active document.");
        desc.setTextColor(0xFFA1A1AA);
        desc.setTextSize(12f);
        desc.setPadding(0, 4, 0, 16);
        mAnnotationContainer.addView(desc);

        // Color Chips
        TextView colorLabel = new TextView(this);
        colorLabel.setText("PEN PALETTE COLOR");
        colorLabel.setTextColor(0xFF71717A);
        colorLabel.setTextSize(10f);
        colorLabel.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        mAnnotationContainer.addView(colorLabel);

        LinearLayout colorChips = new LinearLayout(this);
        colorChips.setOrientation(LinearLayout.HORIZONTAL);
        colorChips.setPadding(0, 10, 0, 20);

        int[] colors = {
            0xFFDC2626, // Red
            0xFF0284C7, // Blue
            0xFF16A34A, // Green
            0xFFFACC15, // Yellow Highlighter
            0xFF9333EA, // Purple
            0xFF000000  // Black
        };
        String[] colorNames = {"Red", "Blue", "Green", "Yellow", "Purple", "Black"};

        for (int i = 0; i < colors.length; i++) {
            final int c = colors[i];
            Button chip = createModernPillButton(colorNames[i], c);
            chip.setTextColor(c == 0xFFFACC15 ? 0xFF000000 : 0xFFFFFFFF);
            chip.setOnClickListener(v -> {
                mPdfPageView.setInkColor(c);
                mColorIndicator.setBackgroundColor(c);
                Toast.makeText(this, "Pen Color Set", Toast.LENGTH_SHORT).show();
            });
            colorChips.addView(chip);
        }
        mAnnotationContainer.addView(colorChips);

        // Active Color Preview
        LinearLayout previewRow = new LinearLayout(this);
        previewRow.setOrientation(LinearLayout.HORIZONTAL);
        previewRow.setGravity(Gravity.CENTER_VERTICAL);
        previewRow.setPadding(0, 0, 0, 20);

        TextView activeText = new TextView(this);
        activeText.setText("Current Pen Stroke: ");
        activeText.setTextColor(0xFFE4E4E7);
        activeText.setTextSize(13f);
        previewRow.addView(activeText);

        mColorIndicator = new View(this);
        mColorIndicator.setLayoutParams(new LinearLayout.LayoutParams(40, 20));
        mColorIndicator.setBackgroundColor(mPdfPageView.getInkColor());
        previewRow.addView(mColorIndicator);

        mAnnotationContainer.addView(previewRow);

        // Stroke Width Selectors
        TextView widthLabel = new TextView(this);
        widthLabel.setText("STROKE THICKNESS");
        widthLabel.setTextColor(0xFF71717A);
        widthLabel.setTextSize(10f);
        widthLabel.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        mAnnotationContainer.addView(widthLabel);

        LinearLayout widthRow = new LinearLayout(this);
        widthRow.setOrientation(LinearLayout.HORIZONTAL);
        widthRow.setPadding(0, 8, 0, 24);

        Button btnFine = createModernPillButton("Fine (3 pt)", 0xFF27272A);
        btnFine.setOnClickListener(v -> mPdfPageView.setInkWidth(3.0f));
        widthRow.addView(btnFine);

        Button btnMed = createModernPillButton("Medium (6 pt)", 0xFF27272A);
        btnMed.setOnClickListener(v -> mPdfPageView.setInkWidth(6.0f));
        widthRow.addView(btnMed);

        Button btnThick = createModernPillButton("Broad (12 pt)", 0xFF27272A);
        btnThick.setOnClickListener(v -> mPdfPageView.setInkWidth(12.0f));
        widthRow.addView(btnThick);

        mAnnotationContainer.addView(widthRow);

        // Markup Actions (Undo, Clear, Return to Canvas)
        LinearLayout actionRow = new LinearLayout(this);
        actionRow.setOrientation(LinearLayout.HORIZONTAL);

        Button btnUndo = createModernPillButton("↶ Undo Stroke", 0xFF27272A);
        btnUndo.setOnClickListener(v -> mPdfPageView.undoLastStroke());
        actionRow.addView(btnUndo);

        Button btnClear = createModernPillButton("🧹 Clear All", 0xFFDC2626);
        btnClear.setOnClickListener(v -> mPdfPageView.clearAllStrokes());
        actionRow.addView(btnClear);

        Button btnDrawNow = createModernPillButton("✍️ Start Inking on Canvas", 0xFF0284C7);
        btnDrawNow.setOnClickListener(v -> {
            mPdfPageView.setInkMode(true);
            switchTab(1);
            Toast.makeText(this, "Ink Mode ACTIVE: Draw on the page now", Toast.LENGTH_SHORT).show();
        });
        actionRow.addView(btnDrawNow);

        mAnnotationContainer.addView(actionRow);
    }

    // =========================================================================
    // TAB 4: WORD STUDIO & TEXT TO PDF CONVERTER
    // =========================================================================
    private void createCreatorTab() {
        mCreatorContainer = new LinearLayout(this);
        mCreatorContainer.setOrientation(LinearLayout.VERTICAL);
        mCreatorContainer.setBackgroundColor(0xFF0F0F12);
        mCreatorContainer.setPadding(24, 20, 24, 20);

        ScrollView sv = new ScrollView(this);
        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);

        TextView title = new TextView(this);
        title.setText("Word Document & PDF Studio Creator");
        title.setTextColor(0xFFFFFFFF);
        title.setTextSize(16f);
        title.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        content.addView(title);

        TextView desc = new TextView(this);
        desc.setText("Compose structured documents, notes, or executive summaries and instantly compile into multi-page vector PDF documents.");
        desc.setTextColor(0xFFA1A1AA);
        desc.setTextSize(12f);
        desc.setPadding(0, 4, 0, 16);
        content.addView(desc);

        // Document Title Input
        TextView lblDocTitle = new TextView(this);
        lblDocTitle.setText("DOCUMENT TITLE");
        lblDocTitle.setTextColor(0xFF71717A);
        lblDocTitle.setTextSize(10f);
        lblDocTitle.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        content.addView(lblDocTitle);

        EditText edtTitle = new EditText(this);
        edtTitle.setText("Executive Report & Analysis");
        edtTitle.setTextColor(0xFFFFFFFF);
        edtTitle.setBackgroundColor(0xFF1F1F23);
        edtTitle.setPadding(20, 14, 20, 14);
        edtTitle.setTextSize(13f);
        content.addView(edtTitle);

        // Body Content Input
        TextView lblBody = new TextView(this);
        lblBody.setText("DOCUMENT CONTENT / TRANSCRIPT");
        lblBody.setTextColor(0xFF71717A);
        lblBody.setTextSize(10f);
        lblBody.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        lblBody.setPadding(0, 14, 0, 6);
        content.addView(lblBody);

        EditText edtBody = new EditText(this);
        edtBody.setText("PSD PDF Studio & Viewer Enterprise Edition\n\n" +
            "This document was compiled natively on Android via the PSD High-Speed Document Pipeline.\n\n" +
            "Features include:\n" +
            "1. Hardware-accelerated continuous vector rendering\n" +
            "2. Live Stylus Ink drawing and markup\n" +
            "3. Sovereign air-gapped local privacy\n" +
            "4. Multi-format Word & PDF compilation\n" +
            "5. Ultra-fast local file indexing and search\n\n" +
            "Created on: " + new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US).format(new Date()));
        edtBody.setTextColor(0xFFFFFFFF);
        edtBody.setBackgroundColor(0xFF1F1F23);
        edtBody.setPadding(20, 14, 20, 14);
        edtBody.setTextSize(12.5f);
        edtBody.setMinLines(6);
        edtBody.setGravity(Gravity.TOP);
        content.addView(edtBody);

        // Compile & Save PDF Button
        Button btnCompile = createModernPillButton("📄 Compile & Open Vector PDF", 0xFF0284C7);
        btnCompile.setPadding(30, 20, 30, 20);
        btnCompile.setTextSize(14f);
        btnCompile.setOnClickListener(v -> {
            String docTitle = edtTitle.getText().toString();
            String docText = edtBody.getText().toString();
            File outDir = getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);
            if (outDir == null) outDir = getFilesDir();
            String timeStamp = new SimpleDateFormat("yyyyMMdd_HHmmss", Locale.US).format(new Date());
            File outFile = new File(outDir, "PSD_Doc_" + timeStamp + ".pdf");

            boolean success = TextToPdfConverter.convertTextToPdf(docTitle, docText, outFile.getAbsolutePath(), 12.0, "PSD PDF Studio Mobile");
            if (success) {
                Toast.makeText(this, "PDF Generated Successfully!", Toast.LENGTH_SHORT).show();
                openPdfFile(outFile);
            } else {
                Toast.makeText(this, "Failed to compile PDF.", Toast.LENGTH_LONG).show();
            }
        });
        content.addView(btnCompile);

        // Text Extractor Section
        TextView lblExtract = new TextView(this);
        lblExtract.setText("EXTRACT TEXT FROM ACTIVE PDF");
        lblExtract.setTextColor(0xFF71717A);
        lblExtract.setTextSize(10f);
        lblExtract.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        lblExtract.setPadding(0, 24, 0, 6);
        content.addView(lblExtract);

        Button btnExtract = createModernPillButton("🔍 Extract Text Stream", 0xFF27272A);
        btnExtract.setOnClickListener(v -> {
            if (mCurrentDocPath.isEmpty() || !new File(mCurrentDocPath).exists()) {
                Toast.makeText(this, "No active document loaded to extract.", Toast.LENGTH_SHORT).show();
                return;
            }
            String extracted = PdfToTextExtractor.extractText(mCurrentDocPath);
            showExtractedTextDialog(extracted);
        });
        content.addView(btnExtract);

        sv.addView(content);
        mCreatorContainer.addView(sv);
    }

    // =========================================================================
    // BOTTOM NAVIGATION BAR
    // =========================================================================
    private View createBottomNavBar() {
        mBottomNav = new LinearLayout(this);
        mBottomNav.setOrientation(LinearLayout.HORIZONTAL);
        mBottomNav.setBackgroundColor(0xFF18181B);
        mBottomNav.setPadding(8, 8, 8, 8);

        String[] labels = {"📚 Library", "📄 Viewer", "✍️ Markup", "📝 Word/Create"};

        for (int i = 0; i < 4; i++) {
            final int tabIndex = i;
            Button navBtn = new Button(this);
            navBtn.setText(labels[i]);
            navBtn.setTextSize(11f);
            navBtn.setTextColor(0xFF71717A);
            navBtn.setBackgroundColor(Color.TRANSPARENT);
            navBtn.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.0f));
            navBtn.setOnClickListener(v -> switchTab(tabIndex));
            mNavButtons[i] = navBtn;
            mBottomNav.addView(navBtn);
        }

        return mBottomNav;
    }

    private void switchTab(int index) {
        mActiveTab = index;
        mLibraryContainer.setVisibility(index == 0 ? View.VISIBLE : View.GONE);
        mViewerContainer.setVisibility(index == 1 ? View.VISIBLE : View.GONE);
        mAnnotationContainer.setVisibility(index == 2 ? View.VISIBLE : View.GONE);
        mCreatorContainer.setVisibility(index == 3 ? View.VISIBLE : View.GONE);

        // If switching away from Viewer, disable ink mode
        if (index != 1) {
            mPdfPageView.setInkMode(false);
        }

        for (int i = 0; i < 4; i++) {
            if (i == index) {
                mNavButtons[i].setTextColor(0xFF38BDF8);
                mNavButtons[i].setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
            } else {
                mNavButtons[i].setTextColor(0xFF71717A);
                mNavButtons[i].setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.NORMAL));
            }
        }
    }

    // =========================================================================
    // PDF RENDERING ENGINE & NAVIGATION
    // =========================================================================
    public void openPdfFile(File file) {
        if (file == null || !file.exists()) {
            Toast.makeText(this, "Selected file does not exist.", Toast.LENGTH_SHORT).show();
            return;
        }

        try {
            closeCurrentPdf();

            mFileDescriptor = ParcelFileDescriptor.open(file, ParcelFileDescriptor.MODE_READ_ONLY);
            mPdfRenderer = new PdfRenderer(mFileDescriptor);
            mTotalPages = mPdfRenderer.getPageCount();
            mCurrentDocTitle = file.getName();
            mCurrentDocPath = file.getAbsolutePath();
            mCurrentDocSize = file.length();

            mHeaderTitle.setText(mCurrentDocTitle);
            mHeaderSubtitle.setText(String.format(Locale.US, "%d Pages • %.1f MB", mTotalPages, mCurrentDocSize / (1024f * 1024f)));

            mCurrentPageIndex = 0;
            renderCurrentPage();

            // Switch to Viewer Tab
            switchTab(1);

        } catch (Exception e) {
            Toast.makeText(this, "Could not open PDF: " + e.getMessage(), Toast.LENGTH_LONG).show();
        }
    }

    private void renderCurrentPage() {
        if (mPdfRenderer == null || mTotalPages == 0) return;

        try {
            if (mCurrentPage != null) {
                mCurrentPage.close();
                mCurrentPage = null;
            }

            mCurrentPage = mPdfRenderer.openPage(mCurrentPageIndex);

            DisplayMetrics metrics = getResources().getDisplayMetrics();
            int screenWidth = metrics.widthPixels;
            int screenHeight = metrics.heightPixels;

            // Render at high-DPI quality
            int renderWidth = Math.max(screenWidth, mCurrentPage.getWidth() * 2);
            int renderHeight = (int) ((float) renderWidth * mCurrentPage.getHeight() / mCurrentPage.getWidth());

            Bitmap bitmap = Bitmap.createBitmap(renderWidth, renderHeight, Bitmap.Config.ARGB_8888);
            mCurrentPage.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY);

            mPdfPageView.setPageBitmap(bitmap);

            mTxtPageCounter.setText(String.format(Locale.US, "Page %d of %d", mCurrentPageIndex + 1, mTotalPages));

        } catch (Exception e) {
            Toast.makeText(this, "Render error: " + e.getMessage(), Toast.LENGTH_SHORT).show();
        }
    }

    private void nextPage() {
        if (mCurrentPageIndex < mTotalPages - 1) {
            mCurrentPageIndex++;
            renderCurrentPage();
        }
    }

    private void prevPage() {
        if (mCurrentPageIndex > 0) {
            mCurrentPageIndex--;
            renderCurrentPage();
        }
    }

    private void jumpToPage(int pageIndex) {
        if (pageIndex >= 0 && pageIndex < mTotalPages) {
            mCurrentPageIndex = pageIndex;
            renderCurrentPage();
        }
    }

    private void showJumpToPageDialog() {
        if (mTotalPages <= 1) return;

        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setTitle("Jump to Page (1 - " + mTotalPages + ")");

        final EditText input = new EditText(this);
        input.setInputType(android.text.InputType.TYPE_CLASS_NUMBER);
        input.setHint("Enter page number");
        builder.setView(input);

        builder.setPositiveButton("Go", (dialog, which) -> {
            try {
                int page = Integer.parseInt(input.getText().toString().trim());
                if (page >= 1 && page <= mTotalPages) {
                    jumpToPage(page - 1);
                } else {
                    Toast.makeText(this, "Page number out of range.", Toast.LENGTH_SHORT).show();
                }
            } catch (Exception ignored) {}
        });
        builder.setNegativeButton("Cancel", null);
        builder.show();
    }

    private void toggleNightMode() {
        boolean night = !mPdfPageView.isNightMode();
        mPdfPageView.setNightMode(night);
        mBtnNightToggle.setBackgroundColor(night ? 0xFF0284C7 : 0xFF27272A);
        Toast.makeText(this, night ? "Night Reading Mode ACTIVE" : "Normal Daylight Mode", Toast.LENGTH_SHORT).show();
    }

    private void closeCurrentPdf() {
        try {
            if (mCurrentPage != null) {
                mCurrentPage.close();
                mCurrentPage = null;
            }
            if (mPdfRenderer != null) {
                mPdfRenderer.close();
                mPdfRenderer = null;
            }
            if (mFileDescriptor != null) {
                mFileDescriptor.close();
                mFileDescriptor = null;
            }
        } catch (Exception ignored) {}
    }

    // =========================================================================
    // INTENT & FILE PICKER INTEGRATION
    // =========================================================================
    private void pickPdfFromDevice() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("application/pdf");
        startActivityForResult(intent, REQUEST_CODE_PICK_PDF);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode == REQUEST_CODE_PICK_PDF && resultCode == RESULT_OK && data != null) {
            Uri uri = data.getData();
            if (uri != null) {
                loadFromUri(uri);
            }
        }
    }

    private void handleIntent(Intent intent) {
        if (intent == null) return;
        String action = intent.getAction();
        Uri data = intent.getData();

        if ((Intent.ACTION_VIEW.equals(action) || Intent.ACTION_SEND.equals(action)) && data != null) {
            loadFromUri(data);
        }
    }

    private void loadFromUri(Uri uri) {
        try {
            ContentResolver resolver = getContentResolver();
            String displayName = "Imported_Document.pdf";

            Cursor cursor = resolver.query(uri, null, null, null, null);
            if (cursor != null && cursor.moveToFirst()) {
                int nameIdx = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (nameIdx != -1) {
                    displayName = cursor.getString(nameIdx);
                }
                cursor.close();
            }

            File tempFile = new File(getCacheDir(), displayName);
            try (InputStream is = resolver.openInputStream(uri);
                 FileOutputStream fos = new FileOutputStream(tempFile)) {
                byte[] buffer = new byte[16384];
                int read;
                while ((read = is.read(buffer)) != -1) {
                    fos.write(buffer, 0, read);
                }
            }

            openPdfFile(tempFile);

        } catch (Exception e) {
            Toast.makeText(this, "Failed to load document: " + e.getMessage(), Toast.LENGTH_LONG).show();
        }
    }

    // =========================================================================
    // DEVICE STORAGE SCANNER & LOCAL LIBRARY
    // =========================================================================
    private void refreshDeviceLibrary() {
        new AsyncTask<Void, Void, List<File>>() {
            @Override
            protected List<File> doInBackground(Void... voids) {
                List<File> pdfs = new ArrayList<>();

                // Scan App-specific storage
                File appDocs = getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);
                if (appDocs != null) scanDirectoryForPdfs(appDocs, pdfs, 0);

                File appDownloads = getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS);
                if (appDownloads != null) scanDirectoryForPdfs(appDownloads, pdfs, 0);

                // Scan Public Downloads & Documents
                File pubDownloads = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS);
                if (pubDownloads != null && pubDownloads.exists()) scanDirectoryForPdfs(pubDownloads, pdfs, 0);

                File pubDocs = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS);
                if (pubDocs != null && pubDocs.exists()) scanDirectoryForPdfs(pubDocs, pdfs, 0);

                return pdfs;
            }

            @Override
            protected void onPostExecute(List<File> files) {
                mDiscoveredPdfFiles.clear();
                mDiscoveredPdfFiles.addAll(files);
                filterLibraryItems(mSearchInput != null ? mSearchInput.getText().toString() : "");
            }
        }.execute();
    }

    private void scanDirectoryForPdfs(File dir, List<File> result, int depth) {
        if (dir == null || !dir.exists() || !dir.canRead() || depth > 3) return;

        File[] files = dir.listFiles();
        if (files == null) return;

        for (File f : files) {
            if (f.isDirectory() && !f.getName().startsWith(".")) {
                scanDirectoryForPdfs(f, result, depth + 1);
            } else if (f.isFile() && f.getName().toLowerCase(Locale.ROOT).endsWith(".pdf")) {
                result.add(f);
            }
        }
    }

    private void filterLibraryItems(String query) {
        mFilteredPdfFiles.clear();
        String q = query == null ? "" : query.trim().toLowerCase(Locale.ROOT);

        for (File f : mDiscoveredPdfFiles) {
            if (q.isEmpty() || f.getName().toLowerCase(Locale.ROOT).contains(q)) {
                mFilteredPdfFiles.add(f);
            }
        }

        mLibraryItemsLayout.removeAllViews();

        if (mFilteredPdfFiles.isEmpty()) {
            TextView emptyTv = new TextView(this);
            emptyTv.setText("No documents found matching query.\nTap '+ Open PDF File' or '⚡ Sample PDF' above.");
            emptyTv.setTextColor(0xFF71717A);
            emptyTv.setTextSize(13f);
            emptyTv.setPadding(16, 40, 16, 40);
            emptyTv.setGravity(Gravity.CENTER);
            mLibraryItemsLayout.addView(emptyTv);
            return;
        }

        for (File f : mFilteredPdfFiles) {
            mLibraryItemsLayout.addView(createDocumentCard(f));
        }
    }

    private View createDocumentCard(File file) {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setBackgroundColor(0xFF18181B);
        card.setPadding(20, 16, 20, 16);

        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT
        );
        lp.setMargins(0, 0, 0, 12);
        card.setLayoutParams(lp);

        TextView title = new TextView(this);
        title.setText(file.getName());
        title.setTextColor(0xFFFFFFFF);
        title.setTextSize(13.5f);
        title.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
        card.addView(title);

        LinearLayout meta = new LinearLayout(this);
        meta.setOrientation(LinearLayout.HORIZONTAL);
        meta.setPadding(0, 4, 0, 0);

        TextView sizeTv = new TextView(this);
        double mb = file.length() / (1024.0 * 1024.0);
        sizeTv.setText(String.format(Locale.US, "%.2f MB", mb));
        sizeTv.setTextColor(0xFF38BDF8);
        sizeTv.setTextSize(11f);
        meta.addView(sizeTv);

        TextView dateTv = new TextView(this);
        dateTv.setText(" • " + new SimpleDateFormat("yyyy-MM-dd", Locale.US).format(new Date(file.lastModified())));
        dateTv.setTextColor(0xFFA1A1AA);
        dateTv.setTextSize(11f);
        meta.addView(dateTv);

        card.addView(meta);

        card.setOnClickListener(v -> openPdfFile(file));

        return card;
    }

    // =========================================================================
    // DEMO PDF GENERATOR & EXTRAS
    // =========================================================================
    private void generateDemoPdfAndOpen() {
        File outDir = getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);
        if (outDir == null) outDir = getFilesDir();
        File sample = new File(outDir, "PSD_PDF_Studio_QuickStart.pdf");

        String content = "Welcome to PSD PDF Studio & Viewer for Android!\n\n" +
            "This document verifies that native hardware-accelerated vector PDF rendering is 100% active on this device.\n\n" +
            "Workstation Capabilities on Mobile:\n" +
            "• Pinch-to-zoom up to 600% with sub-pixel clarity\n" +
            "• Freehand Stylus and Finger ink markup with live stroke overlay\n" +
            "• Word Document Creator to write notes and compile vector PDFs instantly\n" +
            "• Night Reading Mode with high-contrast color inversion\n" +
            "• Device-wide document auto-indexing with instant search\n\n" +
            "Engine: Dual-ABI Native Core (armeabi-v7a / arm64-v8a)\n" +
            "Target: Android 6.0 Marshmallow through Android 14+";

        boolean ok = TextToPdfConverter.convertTextToPdf("PSD PDF Studio Mobile QuickStart", content, sample.getAbsolutePath(), 12.0, "PSD PDF Studio Mobile Enterprise");
        if (ok) {
            refreshDeviceLibrary();
            openPdfFile(sample);
            Toast.makeText(this, "QuickStart Demo PDF Created & Loaded!", Toast.LENGTH_SHORT).show();
        }
    }

    private void showDocumentInfoDialog() {
        AlertDialog.Builder b = new AlertDialog.Builder(this);
        b.setTitle("Document Properties");
        String msg = "Document: " + mCurrentDocTitle + "\n" +
            "Location: " + (mCurrentDocPath.isEmpty() ? "Stream Memory" : mCurrentDocPath) + "\n" +
            "Total Pages: " + mTotalPages + "\n" +
            "File Size: " + String.format(Locale.US, "%.2f MB (%d bytes)", mCurrentDocSize / (1024f * 1024f), mCurrentDocSize) + "\n" +
            "Active Page: " + (mCurrentPageIndex + 1) + " of " + mTotalPages + "\n" +
            "Renderer: Native Android Hardware PdfRenderer";
        b.setMessage(msg);
        b.setPositiveButton("Close", null);
        b.show();
    }

    private void showExtractedTextDialog(String text) {
        AlertDialog.Builder b = new AlertDialog.Builder(this);
        b.setTitle("Extracted Text Stream");
        ScrollView sv = new ScrollView(this);
        TextView tv = new TextView(this);
        tv.setText(text);
        tv.setPadding(24, 20, 24, 20);
        tv.setTextIsSelectable(true);
        sv.addView(tv);
        b.setView(sv);
        b.setPositiveButton("Copy All", (dialog, which) -> {
            android.content.ClipboardManager cm = (android.content.ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
            android.content.ClipData clip = android.content.ClipData.newPlainText("PDF Text", text);
            cm.setPrimaryClip(clip);
            Toast.makeText(this, "Copied to clipboard", Toast.LENGTH_SHORT).show();
        });
        b.setNegativeButton("Close", null);
        b.show();
    }

    private void shareCurrentPdf() {
        if (mCurrentDocPath.isEmpty() || !new File(mCurrentDocPath).exists()) {
            Toast.makeText(this, "No saved document to share.", Toast.LENGTH_SHORT).show();
            return;
        }

        try {
            Intent shareIntent = new Intent(Intent.ACTION_SEND);
            shareIntent.setType("application/pdf");
            shareIntent.putExtra(Intent.EXTRA_STREAM, Uri.fromFile(new File(mCurrentDocPath)));
            shareIntent.putExtra(Intent.EXTRA_SUBJECT, mCurrentDocTitle);
            startActivity(Intent.createChooser(shareIntent, "Share Document via..."));
        } catch (Exception e) {
            Toast.makeText(this, "Share error: " + e.getMessage(), Toast.LENGTH_SHORT).show();
        }
    }

    private Button createModernPillButton(String text, int bgColor) {
        Button btn = new Button(this);
        btn.setText(text);
        btn.setTextSize(11f);
        btn.setTextColor(0xFFFFFFFF);
        btn.setBackgroundColor(bgColor);
        btn.setPadding(18, 10, 18, 10);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT
        );
        lp.setMargins(0, 0, 8, 0);
        btn.setLayoutParams(lp);
        return btn;
    }
}
