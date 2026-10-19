import 'package:flutter/material.dart';

enum ScanFilterMode {
  magicColor,
  bwOtsu,
  grayscale,
  original,
}

class FilterSelectionSheet extends StatefulWidget {
  final Function(ScanFilterMode filter, int qualityLevel) onFilterApplied;

  const FilterSelectionSheet({Key? key, required this.onFilterApplied}) : super(key: key);

  @override
  State<FilterSelectionSheet> createState() => _FilterSelectionSheetState();
}

class _FilterSelectionSheetState extends State<FilterSelectionSheet> {
  ScanFilterMode _selectedFilter = ScanFilterMode.magicColor;
  double _qualityValue = 2.0; // 0=Low, 1=Medium, 2=High

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: const BoxDecoration(
        color: Color(0xFF202020),
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                "Document Scan Filters",
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Colors.white),
              ),
              IconButton(
                icon: const Icon(Icons.close, color: Colors.white70),
                onPressed: () => Navigator.of(context).pop(),
              ),
            ],
          ),
          const SizedBox(height: 16),

          // Filter option cards
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceEvenly,
            children: [
              _buildFilterOption(
                label: "Magic Color",
                mode: ScanFilterMode.magicColor,
                icon: Icons.auto_fix_high,
                color: Colors.amber,
              ),
              _buildFilterOption(
                label: "B&W Otsu",
                mode: ScanFilterMode.bwOtsu,
                icon: Icons.contrast,
                color: Colors.white,
              ),
              _buildFilterOption(
                label: "Grayscale",
                mode: ScanFilterMode.grayscale,
                icon: Icons.filter_b_and_w,
                color: Colors.grey,
              ),
              _buildFilterOption(
                label: "Original",
                mode: ScanFilterMode.original,
                icon: Icons.photo,
                color: Colors.blueAccent,
              ),
            ],
          ),

          const SizedBox(height: 24),
          const Text(
            "Compression & JBIG2 / Flate Footprint",
            style: TextStyle(color: Colors.white70, fontSize: 13, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 8),

          Row(
            children: [
              const Text("Low", style: TextStyle(color: Colors.white54, fontSize: 11)),
              Expanded(
                child: Slider(
                  value: _qualityValue,
                  min: 0.0,
                  max: 2.0,
                  divisions: 2,
                  activeColor: Colors.blueAccent,
                  onChanged: (val) {
                    setState(() {
                      _qualityValue = val;
                    });
                  },
                ),
              ),
              const Text("High", style: TextStyle(color: Colors.white54, fontSize: 11)),
            ],
          ),
          Text(
            _qualityValue == 0
                ? "Low Quality: Max compression (< 50KB/page)"
                : _qualityValue == 1
                    ? "Medium Quality: Balanced JBIG2/Flate (< 80KB/page)"
                    : "High Quality: Archival print-ready (< 120KB/page)",
            style: const TextStyle(color: Colors.white38, fontSize: 11),
          ),

          const SizedBox(height: 24),
          SizedBox(
            width: double.infinity,
            height: 44,
            child: ElevatedButton.icon(
              icon: const Icon(Icons.picture_as_pdf),
              label: const Text("Apply Filter & Add Page to PDF"),
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.blueAccent,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
              ),
              onPressed: () {
                widget.onFilterApplied(_selectedFilter, _qualityValue.toInt());
                Navigator.of(context).pop();
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildFilterOption({
    required String label,
    required ScanFilterMode mode,
    required IconData icon,
    required Color color,
  }) {
    final isSelected = _selectedFilter == mode;

    return GestureDetector(
      onTap: () {
        setState(() {
          _selectedFilter = mode;
        });
      },
      child: Container(
        width: 75,
        padding: const EdgeInsets.symmetric(vertical: 12),
        decoration: BoxDecoration(
          color: isSelected ? Colors.blueAccent.withOpacity(0.25) : const Color(0xFF2C2C2C),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(
            color: isSelected ? Colors.blueAccent : Colors.white10,
            width: isSelected ? 2 : 1,
          ),
        ),
        child: Column(
          children: [
            Icon(icon, color: color, size: 28),
            const SizedBox(height: 8),
            Text(
              label,
              style: TextStyle(
                fontSize: 10,
                color: isSelected ? Colors.white : Colors.white60,
                fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
