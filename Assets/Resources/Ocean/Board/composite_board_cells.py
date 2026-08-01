"""
Composites a new embossed empty-cell texture onto the existing 8x8 board image,
replacing ONLY the interior grid area and leaving the outer frame/border pixels
of the original file completely untouched.

Run from repo root:
    python composite_board_cells.py

Requires: pillow (pip install pillow)
"""
from PIL import Image

BOARD_PATH = "Assets/Resources/Ocean/Board/Board_LightBlue_Final_Square.png"
TILE_PATH = "Assets/Resources/Ocean/Board/EmptyCell_Tile_Pattern.png"
OUTPUT_PATH = "Assets/Resources/Ocean/Board/Board_LightBlue_Final_Square_NEW.png"
BACKUP_PATH = "Assets/Resources/Ocean/Board/Board_LightBlue_Final_Square_BACKUP.png"

# Measured margins from the current board file (do not change without re-measuring)
LEFT_MARGIN = 25
RIGHT_MARGIN = 23
TOP_MARGIN = 15
BOTTOM_MARGIN = 11
GRID_SIZE = 8

def main():
    board = Image.open(BOARD_PATH).convert("RGBA")
    board.save(BACKUP_PATH)  # always keep an untouched backup first

    tile = Image.open(TILE_PATH).convert("RGBA")

    w, h = board.size
    interior_left = LEFT_MARGIN
    interior_top = TOP_MARGIN
    interior_right = w - RIGHT_MARGIN
    interior_bottom = h - BOTTOM_MARGIN
    interior_w = interior_right - interior_left
    interior_h = interior_bottom - interior_top

    cell_w = interior_w / GRID_SIZE
    cell_h = interior_h / GRID_SIZE

    result = board.copy()

    for row in range(GRID_SIZE):
        for col in range(GRID_SIZE):
            x0 = round(interior_left + col * cell_w)
            y0 = round(interior_top + row * cell_h)
            x1 = round(interior_left + (col + 1) * cell_w)
            y1 = round(interior_top + (row + 1) * cell_h)
            cw, ch = x1 - x0, y1 - y0
            cell_img = tile.resize((cw, ch), Image.LANCZOS)
            result.paste(cell_img, (x0, y0), cell_img)

    result.save(OUTPUT_PATH)
    print(f"Saved {OUTPUT_PATH} ({w}x{h}). Backup at {BACKUP_PATH}.")
    print("Review the _NEW file visually before replacing the original.")

if __name__ == "__main__":
    main()
