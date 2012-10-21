﻿/*
*   This file is part of NSMB Editor 5.
*
*   NSMB Editor 5 is free software: you can redistribute it and/or modify
*   it under the terms of the GNU General Public License as published by
*   the Free Software Foundation, either version 3 of the License, or
*   (at your option) any later version.
*
*   NSMB Editor 5 is distributed in the hope that it will be useful,
*   but WITHOUT ANY WARRANTY; without even the implied warranty of
*   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*   GNU General Public License for more details.
*
*   You should have received a copy of the GNU General Public License
*   along with NSMB Editor 5.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace NSMBe4
{
    public partial class TilemapEditorControl : UserControl
    {

        public delegate void TileSelectedd(int tile, bool second);
        public event TileSelectedd TileSelected;


        int hovertx = -1;
        int hoverty = -1;

        int downTileX;
        int downTileY;

        public int selTileX;
        public int selTileY;
        public int selTileWidth;
        public int selTileHeight;

        public Tilemap t;
        public int bufferWidth, bufferHeight;
        public int tileSize;
        public TilePicker picker;

        public ToolStripButton undobutton;
        public ToolStripButton redobutton;
        public Stack<TilemapUndoEntry> UActions = new Stack<TilemapUndoEntry>();
        public Stack<TilemapUndoEntry> RActions = new Stack<TilemapUndoEntry>();

        public enum EditionMode
        {
            DRAW,
            XFLIP,
            YFLIP,
            COPY,
            PASTE,
            CHANGEPAL
        }

        public EditionMode mode;

        public TilemapEditorControl()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.Selectable, true);
        }

        public void load(Tilemap t)
        {
            this.t = t;
            t.render();

            this.tileSize = 8;
            this.bufferHeight = t.height;
            this.bufferWidth = t.width;

            this.Size = this.MinimumSize = new Size(bufferWidth * tileSize, bufferHeight * tileSize);

            undobutton.Click += new EventHandler(Undo);
            redobutton.Click += new EventHandler(Redo);
        }

        int defaultWidth = 1;
        int defaultHeight = 1;

        private void getDefaultSize()
        {
            defaultWidth = 1;
            defaultHeight = 1;

            if (mode == EditionMode.DRAW)
            {
                defaultWidth = picker.selTileWidth;
                defaultHeight = picker.selTileHeight;
            }
            if (mode == EditionMode.PASTE && clipboard != null)
            {
                defaultWidth = clipboardWidth;
                defaultHeight = clipboardHeight;
            }
        }
    
        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (t == null)
                return;

            e.Graphics.FillRectangle(Brushes.DarkSlateGray,
                0, 0, bufferWidth * tileSize, bufferHeight * tileSize);

            e.Graphics.DrawImage(t.buffer, 0, 0);

            getDefaultSize();

            if (down)
            {
                if (selTileWidth == 1 && selTileHeight == 1)
                    e.Graphics.DrawRectangle(Pens.White,
                        selTileX * tileSize, selTileY * tileSize,
                        defaultWidth * tileSize, defaultHeight * tileSize);
                else
                    e.Graphics.DrawRectangle(Pens.White,
                        selTileX * tileSize, selTileY * tileSize,
                        selTileWidth * tileSize, selTileHeight * tileSize);
            }

            if (!down && hovertx != -1)
            {
                e.Graphics.DrawRectangle(Pens.White,
                    hovertx * tileSize, hoverty * tileSize,
                    defaultWidth * tileSize, defaultHeight * tileSize);
            }
        }

        bool down = false;

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            down = true;

            int tx = e.X / tileSize;
            if (tx >= bufferWidth) tx = bufferWidth-1;
            if (tx < 0) tx = 0;
            int ty = e.Y / tileSize;
            if (ty >= bufferHeight) ty = bufferHeight - 1;
            if (ty < 0) ty = 0;

            downTileX = tx;
            downTileY = ty;

            pictureBox1_MouseMove(sender, e);

            this.Focus();
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (down)
            {
                int dx = downTileX;
                int dy = downTileY;

                int tx = e.X / tileSize;
                if (tx >= bufferWidth) tx = bufferWidth - 1;
                if (tx < 0) tx = 0;

                int ty = e.Y / tileSize;
                if (ty >= bufferHeight) ty = bufferHeight - 1;
                if (ty < 0) ty = 0;

                int xmin = Math.Min(dx, tx);
                int ymin = Math.Min(dy, ty);
                int xmax = Math.Max(dx, tx);
                int ymax = Math.Max(dy, ty);

                selTileX = xmin;
                selTileY = ymin;
                selTileWidth = xmax - xmin + 1;
                selTileHeight = ymax - ymin + 1;
            }
            else
            {
                int tx = e.X / tileSize;
                if (tx >= bufferWidth) tx = bufferWidth - 1;
                if (tx < 0) tx = 0;
                int ty = e.Y / tileSize;
                if (ty >= bufferHeight) ty = bufferHeight - 1;
                if (ty < 0) ty = 0;

                hovertx = tx;
                hoverty = ty;
            }
            pictureBox1.Invalidate(true);
        }

        Tilemap.Tile[,] clipboard;
        int clipboardWidth, clipboardHeight;

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (down)
            {
                if (selTileWidth == 1 && selTileHeight == 1)
                {
                    getDefaultSize();
                    selTileWidth = defaultWidth;
                    selTileHeight = defaultHeight;
                }

                undobutton.Enabled = true;
                redobutton.Enabled = false;
                RActions.Clear();
                TilemapUndoEntry item = new TilemapUndoEntry(t.tiles, selTileX, selTileY, selTileWidth, selTileHeight);
                UActions.Push(item);

                switch (mode)
                {
                    case EditionMode.DRAW:
                        for (int x = 0; x < selTileWidth; x++)
                            for (int y = 0; y < selTileHeight; y++)
                            {
                                int tnum = picker.selTileNum;
                                tnum += x % picker.selTileWidth;
                                tnum += (y % picker.selTileHeight) * picker.bufferWidth;

                                if (x + selTileX >= t.width) continue;
                                if (y + selTileY >= t.height) continue;
                                t.tiles[x + selTileX, y + selTileY].tileNum = tnum;
                                t.tiles[x + selTileX, y + selTileY].palNum = picker.selTilePal;
                                t.tiles[x + selTileX, y + selTileY].hflip = false;
                                t.tiles[x + selTileX, y + selTileY].vflip = false;
                            }
                        break;
                    case EditionMode.XFLIP:
                        for (int x = 0; x < selTileWidth / 2; x++)
                            for (int y = 0; y < selTileHeight; y++)
                            {
                                int x1 = x + selTileX;
                                int x2 = selTileX + selTileWidth - x - 1;
                                int yy = y + selTileY;

                                Tilemap.Tile tmp = t.tiles[x1, yy];
                                t.tiles[x1, yy] = t.tiles[x2, yy];
                                t.tiles[x2, yy] = tmp;
                            }
                        for (int x = 0; x < selTileWidth; x++)
                            for (int y = 0; y < selTileHeight; y++)
                                t.tiles[x + selTileX, y + selTileY].hflip = !t.tiles[x + selTileX, y + selTileY].hflip;
                        break;
                    case EditionMode.YFLIP:
                        for (int x = 0; x < selTileWidth; x++)
                            for (int y = 0; y < selTileHeight / 2; y++)
                            {
                                int y1 = y + selTileY;
                                int y2 = selTileY + selTileHeight - y - 1;
                                int xx = x + selTileX;

                                Tilemap.Tile tmp = t.tiles[xx, y1];
                                t.tiles[xx, y1] = t.tiles[xx, y2];
                                t.tiles[xx, y2] = tmp;
                            }
                        for (int x = 0; x < selTileWidth; x++)
                            for (int y = 0; y < selTileHeight; y++)
                                t.tiles[x + selTileX, y + selTileY].vflip = !t.tiles[x + selTileX, y + selTileY].vflip;
                        break;
                    case EditionMode.COPY:
                        clipboard = new Tilemap.Tile[selTileWidth, selTileHeight];
                        clipboardWidth = selTileWidth;
                        clipboardHeight = selTileHeight;

                        for (int x = 0; x < selTileWidth; x++)
                            for (int y = 0; y < selTileHeight; y++)
                                clipboard[x, y] = t.tiles[x + selTileX, y + selTileY];
                        break;
                    case EditionMode.PASTE:
                    	if(clipboard == null) break;
                        for (int x = 0; x < selTileWidth; x++)
                            for (int y = 0; y < selTileHeight; y++)
                                t.tiles[x + selTileX, y + selTileY] = clipboard[x % clipboardWidth, y % clipboardHeight];
                        break;
                    case EditionMode.CHANGEPAL:
                        for (int x = 0; x < selTileWidth; x++)
                            for (int y = 0; y < selTileHeight; y++)
                            {
                                t.tiles[x + selTileX, y + selTileY].palNum++;
                                t.tiles[x + selTileX, y + selTileY].palNum %= t.palettes.Length;
                            } 
                        break;
                }
                t.reRender(selTileX, selTileY, selTileWidth, selTileHeight);
                pictureBox1.Invalidate(true);
                item.LoadNewTiles(t.tiles);
            }
            down = false;
        }

        private void pictureBox1_MouseLeave(object sender, EventArgs e)
        {
            hovertx = -1;
            hoverty = -1;
            pictureBox1.Invalidate(true);
        }

        public void Undo(object sender, EventArgs e)
        {
            if (UActions.Count == 0) return;
            TilemapUndoEntry item = UActions.Pop();
            item.Undo(t.tiles);
            RActions.Push(item);
            undobutton.Enabled = UActions.Count > 0;
            redobutton.Enabled = true;
            pictureBox1.Invalidate(true);
        }

        public void Redo(object sender, EventArgs e)
        {
            if (RActions.Count == 0) return;
            TilemapUndoEntry item = RActions.Pop();
            item.Redo(t.tiles);
            UActions.Push(item);
            redobutton.Enabled = RActions.Count > 0;
            undobutton.Enabled = true;
            pictureBox1.Invalidate(true);
        }
    }

    public class TilemapUndoEntry
    {
        public Tilemap.Tile[,] oldTiles;
        public Tilemap.Tile[,] newTiles;
        public int x;
        public int y;
        public int width;
        public int height;

        public TilemapUndoEntry(Tilemap.Tile[,] allTiles, int x, int y, int width, int height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            oldTiles = getTiles(allTiles);
        }

        public void LoadNewTiles(Tilemap.Tile[,] allTiles)
        {
            newTiles = getTiles(allTiles);
        }

        public void Undo(Tilemap.Tile[,] allTiles)
        {
            setTiles(oldTiles, allTiles);
        }

        public void Redo(Tilemap.Tile[,] allTiles)
        {
            setTiles(newTiles, allTiles);
        }

        public Tilemap.Tile[,] getTiles(Tilemap.Tile[,] allTiles)
        {
            Tilemap.Tile[,] tiles = new Tilemap.Tile[width, height];
            for (int yy = 0; yy < height; yy++)
                for (int xx = 0; xx < width; xx++)
                    tiles[xx, yy] = allTiles[xx + x, yy + y];
            return tiles;
        }

        public void setTiles(Tilemap.Tile[,] tiles, Tilemap.Tile[,] allTiles)
        {
            for (int yy = 0; yy < height; yy++)
                for (int xx = 0; xx < width; xx++)
                {
                    Console.Out.WriteLine(allTiles[xx + x, yy + y].tileNum.ToString() + "; " + tiles[xx, yy].tileNum.ToString());
                    allTiles[xx + x, yy + y] = tiles[xx, yy];
                    Console.Out.WriteLine(allTiles[xx + x, yy + y].tileNum.ToString() + "; " + tiles[xx, yy].tileNum.ToString());
                }
        }
    }
}
