﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageGlass.Gallery;


public partial class ImageListView
{
    /// <summary>
    /// Represents details of keyboard and mouse navigation events.
    /// </summary>
    internal class ImageListViewNavigationManager : IDisposable
    {
        #region Member Variables
        private ImageListView mImageListView;

        private bool inItemArea;
        private bool overCheckBox;

        private Point lastViewOffset;
        private Point lastMouseDownLocation;
        private Dictionary<ImageListViewItem, bool> highlightedItems;

        private bool lastMouseDownInItemArea;

        private bool lastMouseDownOverItem;
        private bool lastMouseDownOverCheckBox;

        private bool selfDragging;

        private System.Windows.Forms.Timer scrollTimer;
        #endregion

        #region Properties
        /// <summary>
        /// Gets whether the left mouse button is down.
        /// </summary>
        public bool LeftButton { get; private set; }
        
        /// <summary>
        /// Gets whether the right mouse button is down.
        /// </summary>
        public bool RightButton { get; private set; }
        
        /// <summary>
        /// Gets whether the shift key is down.
        /// </summary>
        public bool ShiftKey { get; private set; }
        
        /// <summary>
        /// Gets whether the control key is down.
        /// </summary>
        public bool ControlKey { get; private set; }

        /// <summary>
        /// Gets the item under the mouse.
        /// </summary>
        public ImageListViewItem? HoveredItem { get; private set; }

        /// <summary>
        /// Gets whether a mouse selection is in progress.
        /// </summary>
        public bool MouseSelecting { get; private set; }

        /// <summary>
        /// Gets the target item for a drop operation.
        /// </summary>
        public ImageListViewItem? DropTarget { get; private set; }

        /// <summary>
        /// Gets whether drop target is to the right of the item.
        /// </summary>
        public bool DropToRight { get; private set; }

        /// <summary>
        /// Gets the selection rectangle.
        /// </summary>
        public Rectangle SelectionRectangle { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the ImageListViewNavigationManager class.
        /// </summary>
        /// <param name="owner">The owner control.</param>
        public ImageListViewNavigationManager(ImageListView owner)
        {
            mImageListView = owner;

            LeftButton = false;
            RightButton = false;
            ShiftKey = false;
            ControlKey = false;

            HoveredItem = null;
            MouseSelecting = false;

            DropTarget = null;
            DropToRight = false;
            selfDragging = false;

            highlightedItems = new Dictionary<ImageListViewItem, bool>();

            scrollTimer = new System.Windows.Forms.Timer
            {
                Interval = 100,
                Enabled = false
            };
            scrollTimer.Tick += new EventHandler(scrollTimer_Tick);

        }
        #endregion

        #region Instance Methods
        /// <summary>
        /// Determines whether the item is highlighted.
        /// </summary>
        public ItemHighlightState HighlightState(ImageListViewItem item)
        {
            if (highlightedItems.TryGetValue(item, out bool highlighted))
            {
                if (highlighted)
                    return ItemHighlightState.HighlightedAndSelected;
                else
                    return ItemHighlightState.HighlightedAndUnSelected;
            }
            return ItemHighlightState.NotHighlighted;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, 
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            scrollTimer.Dispose();
        }

        #endregion


        #region Mouse Event Handlers
        /// <summary>
        /// Handles control's MouseDown event.
        /// </summary>
        public void MouseDown(MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != MouseButtons.None)
                LeftButton = true;
            if ((e.Button & MouseButtons.Right) != MouseButtons.None)
                RightButton = true;

            DoHitTest(e.Location);
            lastMouseDownInItemArea = inItemArea;
            lastMouseDownOverItem = HoveredItem != null;
            lastMouseDownOverCheckBox = overCheckBox;

            lastViewOffset = mImageListView.ViewOffset;
            lastMouseDownLocation = e.Location;

            if (HoveredItem is not null)
            {
                HoveredItem.Pressed = true;
            }
        }

        /// <summary>
        /// Handles control's MouseMove event.
        /// </summary>
        public void MouseMove(MouseEventArgs e)
        {
            var oldHoveredItem = HoveredItem;

            DoHitTest(e.Location);

            mImageListView.SuspendPaint();

            // Do we need to scroll the view?
            if (MouseSelecting && mImageListView.ScrollOrientation == ScrollOrientation.VerticalScroll && !scrollTimer.Enabled)
            {
                if (e.Y > mImageListView.ClientRectangle.Bottom)
                {
                    scrollTimer.Tag = -SystemInformation.MouseWheelScrollDelta;
                    scrollTimer.Enabled = true;
                }
                else if (e.Y < mImageListView.ClientRectangle.Top)
                {
                    scrollTimer.Tag = SystemInformation.MouseWheelScrollDelta;
                    scrollTimer.Enabled = true;
                }
            }
            else if (MouseSelecting && mImageListView.ScrollOrientation == ScrollOrientation.HorizontalScroll && !scrollTimer.Enabled)
            {
                if (e.X > mImageListView.ClientRectangle.Right)
                {
                    scrollTimer.Tag = -SystemInformation.MouseWheelScrollDelta;
                    scrollTimer.Enabled = true;
                }
                else if (e.X < mImageListView.ClientRectangle.Left)
                {
                    scrollTimer.Tag = SystemInformation.MouseWheelScrollDelta;
                    scrollTimer.Enabled = true;
                }
            }
            else if (scrollTimer.Enabled && mImageListView.ClientRectangle.Contains(e.Location))
            {
                scrollTimer.Enabled = false;
            }


            if (MouseSelecting)
            {
                if (!ShiftKey && !ControlKey)
                    mImageListView.SelectedItems.Clear(false);

                // Create the selection rectangle
                var viewOffset = mImageListView.ViewOffset;
                var pt1 = new Point(
                    lastMouseDownLocation.X - (viewOffset.X - lastViewOffset.X),
                    lastMouseDownLocation.Y - (viewOffset.Y - lastViewOffset.Y));
                var pt2 = new Point(e.Location.X, e.Location.Y);
                SelectionRectangle = new Rectangle(Math.Min(pt1.X, pt2.X), Math.Min(pt1.Y, pt2.Y), Math.Abs(pt1.X - pt2.X), Math.Abs(pt1.Y - pt2.Y));

                // Determine which items are highlighted
                highlightedItems.Clear();

                // Normalize to item area coordinates
                pt1 = new Point(SelectionRectangle.Left, SelectionRectangle.Top);
                pt2 = new Point(SelectionRectangle.Right, SelectionRectangle.Bottom);
                var itemAreaOffset = new Point(
                    -mImageListView.layoutManager.ItemAreaBounds.Left,
                    -mImageListView.layoutManager.ItemAreaBounds.Top);
                pt1.Offset(itemAreaOffset);
                pt2.Offset(itemAreaOffset);

                int startRow = (int)Math.Floor((Math.Min(pt1.Y, pt2.Y) + viewOffset.Y) /
                    (float)mImageListView.layoutManager.ItemSizeWithMargin.Height);
                int endRow = (int)Math.Floor((Math.Max(pt1.Y, pt2.Y) + viewOffset.Y) /
                    (float)mImageListView.layoutManager.ItemSizeWithMargin.Height);
                int startCol = (int)Math.Floor((Math.Min(pt1.X, pt2.X) + viewOffset.X) /
                    (float)mImageListView.layoutManager.ItemSizeWithMargin.Width);
                int endCol = (int)Math.Floor((Math.Max(pt1.X, pt2.X) + viewOffset.X) /
                    (float)mImageListView.layoutManager.ItemSizeWithMargin.Width);

                if (mImageListView.ScrollOrientation == ScrollOrientation.HorizontalScroll &&
                    (startRow >= 0 || endRow >= 0))
                {
                    for (int i = startCol; i <= endCol; i++)
                    {
                        if (i >= 0 && i <= mImageListView.Items.Count - 1 &&
                            !highlightedItems.ContainsKey(mImageListView.Items[i]) &&
                            mImageListView.Items[i].Enabled)
                            highlightedItems.Add(mImageListView.Items[i],
                                (ControlKey ? !mImageListView.Items[i].Selected : true));
                    }
                }
                else if (mImageListView.ScrollOrientation == ScrollOrientation.VerticalScroll &&
                    (startCol >= 0 || endCol >= 0) && (startRow >= 0 || endRow >= 0) &&
                    (startCol <= mImageListView.layoutManager.Cols - 1 || endCol <= mImageListView.layoutManager.Cols - 1))
                {
                    startCol = Math.Min(mImageListView.layoutManager.Cols - 1, Math.Max(0, startCol));
                    endCol = Math.Min(mImageListView.layoutManager.Cols - 1, Math.Max(0, endCol));
                    for (int row = startRow; row <= endRow; row++)
                    {
                        for (int col = startCol; col <= endCol; col++)
                        {
                            int i = row * mImageListView.layoutManager.Cols + col;
                            if (i >= 0 && i <= mImageListView.Items.Count - 1 &&
                                !highlightedItems.ContainsKey(mImageListView.Items[i]) &&
                                mImageListView.Items[i].Enabled)
                                highlightedItems.Add(mImageListView.Items[i],
                                    (ControlKey ? !mImageListView.Items[i].Selected : true));
                        }
                    }
                }


                mImageListView.Refresh();
            }
            else if (!MouseSelecting && inItemArea && lastMouseDownInItemArea &&
                (LeftButton || RightButton) &&
                (Math.Abs(e.Location.X - lastMouseDownLocation.X) > SystemInformation.DragSize.Width ||
                Math.Abs(e.Location.Y - lastMouseDownLocation.Y) > SystemInformation.DragSize.Height))
            {
                if (mImageListView.MultiSelect && !lastMouseDownOverItem && HoveredItem == null)
                {
                    // Start mouse selection
                    MouseSelecting = true;
                    SelectionRectangle = new Rectangle(lastMouseDownLocation, new Size(0, 0));
                    mImageListView.Refresh();
                }
                else if (lastMouseDownOverItem && HoveredItem != null && (mImageListView.AllowItemReorder || mImageListView.AllowDrag))
                {
                    // Start drag&drop
                    if (!HoveredItem.Selected)
                    {
                        mImageListView.SelectedItems.Clear(false);
                        HoveredItem.mSelected = true;
                        mImageListView.OnSelectionChangedInternal();
                        DropTarget = null;
                        mImageListView.Refresh(true);
                    }

                    DropTarget = null;

                    selfDragging = true;
                    bool oldAllowDrop = mImageListView.AllowDrop;
                    mImageListView.AllowDrop = true;
                    if (mImageListView.AllowDrag)
                    {
                        // Set drag data
                        var filenames = new List<string>();
                        foreach (var item in mImageListView.SelectedItems)
                        {
                            // Get the source image
                            var sourceFile = item.Adaptor.GetSourceImage(item.VirtualItemKey);
                            if (!string.IsNullOrEmpty(sourceFile))
                                filenames.Add(sourceFile);
                        }
                        var data = new DataObject(DataFormats.FileDrop, filenames.ToArray());
                        mImageListView.DoDragDrop(data, DragDropEffects.All);
                    }
                    else
                    {
                        mImageListView.DoDragDrop(new object(), DragDropEffects.Move);
                    }
                    mImageListView.AllowDrop = oldAllowDrop;
                    selfDragging = false;

                    // Since the MouseUp event will be eaten by DoDragDrop we will not receive
                    // the MouseUp event. We need to manually update mouse button flags after
                    // the drop.
                    if ((MouseButtons & MouseButtons.Left) == MouseButtons.None)
                        LeftButton = false;
                    if ((MouseButtons & MouseButtons.Right) == MouseButtons.None)
                        RightButton = false;
                }
            }

            else if (!ReferenceEquals(HoveredItem, oldHoveredItem))
            {
                // Hovered item changed
                if (!ReferenceEquals(HoveredItem, oldHoveredItem))
                    mImageListView.OnItemHover(new ItemHoverEventArgs(HoveredItem, oldHoveredItem));

                mImageListView.Refresh();
            }

            mImageListView.ResumePaint();
        }

        /// <summary>
        /// Handles control's MouseUp event.
        /// </summary>
        public void MouseUp(MouseEventArgs e)
        {
            DoHitTest(e.Location);

            mImageListView.SuspendPaint();

            // Stop if we are scrolling
            if (scrollTimer.Enabled)
                scrollTimer.Enabled = false;

            if (MouseSelecting)
            {
                // Apply highlighted items
                if (highlightedItems.Count != 0)
                {
                    foreach (KeyValuePair<ImageListViewItem, bool> pair in highlightedItems)
                    {
                        if (pair.Key.Enabled)
                            pair.Key.mSelected = pair.Value;
                    }
                    highlightedItems.Clear();
                }

                mImageListView.OnSelectionChangedInternal();

                MouseSelecting = false;

                mImageListView.Refresh();
            }
            else if (mImageListView.AllowCheckBoxClick && lastMouseDownInItemArea &&
                lastMouseDownOverCheckBox && HoveredItem != null && overCheckBox && LeftButton)
            {
                if (HoveredItem.Selected)
                {
                    // if multiple items selected and Hovered item among selected,
                    // then give all selected check state !HoveredItem.Checked
                    bool check = !HoveredItem.Checked;
                    foreach (ImageListViewItem item in mImageListView.Items)
                    {
                        if (item.Selected)
                            item.Checked = check;
                    }
                }
                else
                {
                    // if multiple items selected and HoveredItem NOT among selected,
                    // or if only HoveredItem selected or hovered
                    // then toggle HoveredItem.Checked
                    HoveredItem.Checked = !HoveredItem.Checked;
                }
                mImageListView.Refresh();
            }
            else if (lastMouseDownInItemArea && lastMouseDownOverItem && HoveredItem != null && LeftButton)
            {
                // Select the item under the cursor
                if (!mImageListView.MultiSelect && ControlKey)
                {
                    bool oldSelected = HoveredItem.Selected;
                    mImageListView.SelectedItems.Clear(false);
                    HoveredItem.mSelected = !oldSelected;
                }
                else if (!mImageListView.MultiSelect)
                {
                    mImageListView.SelectedItems.Clear(false);
                    HoveredItem.mSelected = true;
                }
                else if (ControlKey)
                {
                    HoveredItem.mSelected = !HoveredItem.mSelected;
                }
                else if (ShiftKey)
                {
                    int startIndex = 0;
                    if (mImageListView.SelectedItems.Count != 0)
                    {
                        startIndex = mImageListView.SelectedItems[0].Index;
                        mImageListView.SelectedItems.Clear(false);
                    }
                    int endIndex = HoveredItem.Index;
                    if (mImageListView.ScrollOrientation == ScrollOrientation.VerticalScroll)
                    {
                        int startRow = Math.Min(startIndex, endIndex) / mImageListView.layoutManager.Cols;
                        int endRow = Math.Max(startIndex, endIndex) / mImageListView.layoutManager.Cols;
                        int startCol = Math.Min(startIndex, endIndex) % mImageListView.layoutManager.Cols;
                        int endCol = Math.Max(startIndex, endIndex) % mImageListView.layoutManager.Cols;

                        for (int row = startRow; row <= endRow; row++)
                        {
                            for (int col = startCol; col <= endCol; col++)
                            {
                                int index = row * mImageListView.layoutManager.Cols + col;
                                mImageListView.Items[index].mSelected = true;
                            }
                        }
                    }
                    else
                    {
                        for (int i = Math.Min(startIndex, endIndex); i <= Math.Max(startIndex, endIndex); i++)
                            mImageListView.Items[i].mSelected = true;
                    }
                }
                else
                {
                    mImageListView.SelectedItems.Clear(false);
                    HoveredItem.mSelected = true;
                }

                // Raise the selection change event
                mImageListView.OnSelectionChangedInternal();
                mImageListView.OnItemClick(new ItemClickEventArgs(HoveredItem, e.Location, e.Button));

                // Set the item as the focused item
                mImageListView.Items.FocusedItem = HoveredItem;

                mImageListView.Refresh();
            }
            else if (lastMouseDownInItemArea && lastMouseDownOverItem && HoveredItem != null && RightButton)
            {
                if (!ControlKey && !HoveredItem.Selected)
                {
                    // Clear the selection if Control key is not pressed
                    mImageListView.SelectedItems.Clear(false);
                    HoveredItem.mSelected = true;
                    mImageListView.OnSelectionChangedInternal();
                }

                mImageListView.OnItemClick(new ItemClickEventArgs(HoveredItem, e.Location, e.Button));
                mImageListView.Items.FocusedItem = HoveredItem;
            }
            else if (lastMouseDownInItemArea && inItemArea && HoveredItem == null && (LeftButton || RightButton))
            {
                // Clear selection if clicked in empty space
                mImageListView.SelectedItems.Clear();
                mImageListView.Refresh();
            }

            if (HoveredItem is not null)
            {
                HoveredItem.Pressed = false;
            }

            if ((e.Button & MouseButtons.Left) != MouseButtons.None)
                LeftButton = false;
            if ((e.Button & MouseButtons.Right) != MouseButtons.None)
                RightButton = false;

            mImageListView.ResumePaint();
        }

        /// <summary>
        /// Handles control's MouseDoubleClick event.
        /// </summary>
        public void MouseDoubleClick(MouseEventArgs e)
        {
            if (lastMouseDownInItemArea && lastMouseDownOverItem && HoveredItem != null)
            {
                mImageListView.OnItemDoubleClick(new ItemClickEventArgs(HoveredItem, e.Location, e.Button));
            }
        }

        /// <summary>
        /// Handles control's MouseLeave event.
        /// </summary>
        public void MouseLeave()
        {
            if (HoveredItem != null)
            {
                if (HoveredItem != null)
                {
                    mImageListView.OnItemHover(new ItemHoverEventArgs(null, HoveredItem));
                    HoveredItem.Pressed = false;
                } 

                HoveredItem = null;
                mImageListView.Refresh();
            }
        }

        #endregion

        #region Key Event Handlers
        /// <summary>
        /// Handles control's KeyDown event.
        /// </summary>
        public void KeyDown(KeyEventArgs e)
        {
            if (!mImageListView.EnableKeyNavigation)
            {
                return;
            }

            ShiftKey = (e.Modifiers & Keys.Shift) == Keys.Shift;
            ControlKey = (e.Modifiers & Keys.Control) == Keys.Control;

            mImageListView.SuspendPaint();

            // If the shift key or the control key is pressed and there is no focused item
            // set the first item as the focused item.
            if ((ShiftKey || ControlKey) && mImageListView.Items.Count != 0 &&
                mImageListView.Items.FocusedItem == null)
            {
                mImageListView.Items.FocusedItem = mImageListView.Items[0];
                mImageListView.Refresh();
            }

            if (mImageListView.Items.Count != 0)
            {
                int index = 0;
                if (mImageListView.Items.FocusedItem != null)
                    index = mImageListView.Items.FocusedItem.Index;

                int newindex = ApplyNavKey(index, e.KeyCode);
                if (index != newindex)
                {
                    if (ControlKey)
                    {
                        // Just move the focus
                    }
                    else if (mImageListView.MultiSelect && ShiftKey)
                    {
                        int startIndex = 0;
                        int endIndex = 0;
                        int selCount = mImageListView.SelectedItems.Count;
                        if (selCount != 0)
                        {
                            startIndex = mImageListView.SelectedItems[0].Index;
                            endIndex = mImageListView.SelectedItems[selCount - 1].Index;
                            mImageListView.SelectedItems.Clear(false);
                        }

                        if (newindex > index) // Moving right or down
                        {
                            if (newindex > endIndex)
                                endIndex = newindex;
                            else
                                startIndex = newindex;
                        }
                        else // Moving left or up
                        {
                            if (newindex < startIndex)
                                startIndex = newindex;
                            else
                                endIndex = newindex;
                        }

                        for (int i = Math.Min(startIndex, endIndex); i <= Math.Max(startIndex, endIndex); i++)
                        {
                            if (mImageListView.Items[i].mEnabled)
                                mImageListView.Items[i].mSelected = true;
                        }
                        mImageListView.OnSelectionChangedInternal();
                    }
                    else if (mImageListView.Items[newindex].mEnabled)
                    {
                        mImageListView.SelectedItems.Clear(false);
                        mImageListView.Items[newindex].mSelected = true;
                        mImageListView.OnSelectionChangedInternal();
                    }
                    mImageListView.Items.FocusedItem = mImageListView.Items[newindex];
                    mImageListView.ScrollToIndex(newindex);
                    mImageListView.Refresh();
                }
            }

            mImageListView.ResumePaint();
        }

        /// <summary>
        /// Handles control's KeyUp event.
        /// </summary>
        public void KeyUp(KeyEventArgs e)
        {
            ShiftKey = (e.Modifiers & Keys.Shift) == Keys.Shift;
            ControlKey = (e.Modifiers & Keys.Control) == Keys.Control;
        }

        #endregion

        #region Drag and Drop Event Handlers
        /// <summary>
        /// Handles control's DragDrop event.
        /// </summary>
        public void DragDrop(DragEventArgs e)
        {
            mImageListView.SuspendPaint();

            if (selfDragging)
            {
                int index = -1;
                if (DropTarget != null) index = DropTarget.Index;
                if (DropToRight) index++;
                if (index > mImageListView.Items.Count)
                    index = mImageListView.Items.Count;

                if (index != -1)
                {
                    int i = 0;
                    ImageListViewItem[] draggedItems = new ImageListViewItem[mImageListView.SelectedItems.Count];
                    foreach (ImageListViewItem item in mImageListView.SelectedItems)
                    {
                        draggedItems[i] = item;
                        i++;
                    }

                    mImageListView.OnDropItems(new DropItemEventArgs(index, draggedItems));
                }
            }
            else
            {
                int index = mImageListView.Items.Count;
                if (DropTarget != null) index = DropTarget.Index;
                if (DropToRight) index++;
                if (index > mImageListView.Items.Count)
                    index = mImageListView.Items.Count;

                if (index != -1)
                {
                    if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        string[] filenames = (string[])e.Data.GetData(DataFormats.FileDrop);

                        mImageListView.OnDropFiles(new DropFileEventArgs(index, filenames));
                    }
                }
            }

            DropTarget = null;
            selfDragging = false;

            mImageListView.Refresh();
            mImageListView.ResumePaint();
        }
        /// <summary>
        /// Handles control's DragEnter event.
        /// </summary>
        public void DragEnter(DragEventArgs e)
        {
            if (selfDragging)
                e.Effect = DragDropEffects.Move;
            else if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }
        /// <summary>
        /// Handles control's DragOver event.
        /// </summary>
        public void DragOver(DragEventArgs e)
        {
            if ((selfDragging && mImageListView.AllowItemReorder) || (!selfDragging && mImageListView.AllowDrop && e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)))
            {
                if (mImageListView.Items.Count == 0)
                {
                    if (selfDragging)
                        e.Effect = DragDropEffects.None;
                    else
                        e.Effect = DragDropEffects.Copy;
                }
                else if (mImageListView.AllowItemReorder)
                {
                    // Calculate the location of the insertion cursor
                    var pt = new Point(e.X, e.Y);
                    pt = mImageListView.PointToClient(pt);

                    // Do we need to scroll the view?
                    if (mImageListView.ScrollOrientation == ScrollOrientation.VerticalScroll &&
                        pt.Y > mImageListView.ClientRectangle.Bottom - 20)
                    {
                        scrollTimer.Tag = -SystemInformation.MouseWheelScrollDelta;
                        scrollTimer.Enabled = true;
                    }
                    else if (mImageListView.ScrollOrientation == ScrollOrientation.VerticalScroll &&
                        pt.Y < mImageListView.ClientRectangle.Top + 20)
                    {
                        scrollTimer.Tag = SystemInformation.MouseWheelScrollDelta;
                        scrollTimer.Enabled = true;
                    }
                    else if (mImageListView.ScrollOrientation == ScrollOrientation.HorizontalScroll &&
                        pt.X > mImageListView.ClientRectangle.Right - 20)
                    {
                        scrollTimer.Tag = -SystemInformation.MouseWheelScrollDelta;
                        scrollTimer.Enabled = true;
                    }
                    else if (mImageListView.ScrollOrientation == ScrollOrientation.HorizontalScroll &&
                        pt.X < mImageListView.ClientRectangle.Left + 20)
                    {
                        scrollTimer.Tag = SystemInformation.MouseWheelScrollDelta;
                        scrollTimer.Enabled = true;
                    }
                    else
                        scrollTimer.Enabled = false;

                    // Normalize to item area coordinates
                    pt.X -= mImageListView.layoutManager.ItemAreaBounds.Left;
                    pt.Y -= mImageListView.layoutManager.ItemAreaBounds.Top;

                    // Row and column mouse is over
                    bool dragCaretOnRight = false;
                    int index = 0;

                    if (mImageListView.ScrollOrientation == ScrollOrientation.HorizontalScroll)
                    {
                        index = (pt.X + mImageListView.ViewOffset.X) / mImageListView.layoutManager.ItemSizeWithMargin.Width;
                    }
                    else
                    {
                        int col = pt.X / mImageListView.layoutManager.ItemSizeWithMargin.Width;
                        int row = (pt.Y + mImageListView.ViewOffset.Y) / mImageListView.layoutManager.ItemSizeWithMargin.Height;
                        if (col > mImageListView.layoutManager.Cols - 1)
                        {
                            col = mImageListView.layoutManager.Cols - 1;
                            dragCaretOnRight = true;
                        }
                        index = row * mImageListView.layoutManager.Cols + col;
                    }

                    if (index < 0) index = 0;
                    if (index > mImageListView.Items.Count - 1)
                    {
                        index = mImageListView.Items.Count - 1;
                        dragCaretOnRight = true;
                    }

                    ImageListViewItem? dragDropTarget = mImageListView.Items[index];

                    if (selfDragging && (dragDropTarget.Selected ||
                        (!dragCaretOnRight && index > 0 && mImageListView.Items[index - 1].Selected) ||
                        (dragCaretOnRight && index < mImageListView.Items.Count - 1 && mImageListView.Items[index + 1].Selected)))
                    {
                        e.Effect = DragDropEffects.None;

                        dragDropTarget = null;
                    }
                    else if (selfDragging)
                        e.Effect = DragDropEffects.Move;
                    else
                        e.Effect = DragDropEffects.Copy;

                    if (!ReferenceEquals(dragDropTarget, DropTarget) || dragCaretOnRight != DropToRight)
                    {
                        DropTarget = dragDropTarget;
                        DropToRight = dragCaretOnRight;
                        mImageListView.Refresh(true);
                    }
                }
                else
                {
                    e.Effect = DragDropEffects.Copy;
                }
            }
            else
                e.Effect = DragDropEffects.None;
        }
        /// <summary>
        /// Handles control's DragLeave event.
        /// </summary>
        public void DragLeave()
        {
            DropTarget = null;
            mImageListView.Refresh(true);

            if (scrollTimer.Enabled)
                scrollTimer.Enabled = false;
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Performs a hit test.
        /// </summary>
        private void DoHitTest(Point pt)
        {
            mImageListView.HitTest(pt, out HitInfo h);

            if (h.ItemHit && mImageListView.Items[h.ItemIndex].Enabled)
            {
                HoveredItem = mImageListView.Items[h.ItemIndex];
            }
            else
            {
                HoveredItem = null;
            }

            inItemArea = h.InItemArea;
            overCheckBox = h.CheckBoxHit;
        }
        /// <summary>
        /// Returns the item index after applying the given navigation key.
        /// </summary>
        private int ApplyNavKey(int index, Keys key)
        {
            if (mImageListView.ScrollOrientation == ScrollOrientation.VerticalScroll)
            {
                if (key == Keys.Up && index >= mImageListView.layoutManager.Cols)
                    index -= mImageListView.layoutManager.Cols;
                else if (key == Keys.Down && index < mImageListView.Items.Count - mImageListView.layoutManager.Cols)
                    index += mImageListView.layoutManager.Cols;
                else if (key == Keys.Left && index > 0)
                    index--;
                else if (key == Keys.Right && index < mImageListView.Items.Count - 1)
                    index++;
                else if (key == Keys.PageUp && index >= mImageListView.layoutManager.Cols * (mImageListView.layoutManager.Rows - 1))
                    index -= mImageListView.layoutManager.Cols * (mImageListView.layoutManager.Rows - 1);
                else if (key == Keys.PageDown && index < mImageListView.Items.Count - mImageListView.layoutManager.Cols * (mImageListView.layoutManager.Rows - 1))
                    index += mImageListView.layoutManager.Cols * (mImageListView.layoutManager.Rows - 1);
                else if (key == Keys.Home)
                    index = 0;
                else if (key == Keys.End)
                    index = mImageListView.Items.Count - 1;
            }
            else
            {
                if (key == Keys.Left && index > 0)
                    index--;
                else if (key == Keys.Right && index < mImageListView.Items.Count - 1)
                    index++;
                else if (key == Keys.PageUp && index >= mImageListView.layoutManager.Cols)
                    index -= mImageListView.layoutManager.Cols;
                else if (key == Keys.PageDown && index < mImageListView.Items.Count - mImageListView.layoutManager.Cols)
                    index += mImageListView.layoutManager.Cols;
                else if (key == Keys.Home)
                    index = 0;
                else if (key == Keys.End)
                    index = mImageListView.Items.Count - 1;
            }

            if (index < 0)
                index = 0;
            else if (index > mImageListView.Items.Count - 1)
                index = mImageListView.Items.Count - 1;

            return index;
        }
        #endregion

        #region Scroll Timer
        /// <summary>
        /// Handles the Tick event of the scrollTimer control.
        /// </summary>
        private void scrollTimer_Tick(object? sender, EventArgs e)
        {
            int delta = (int)scrollTimer.Tag;
            Point location = mImageListView.PointToClient(MousePosition);
            mImageListView.OnMouseMove(new MouseEventArgs(MouseButtons, 0, location.X, location.Y, 0));
            mImageListView.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, delta));
        }
        #endregion
    }

}