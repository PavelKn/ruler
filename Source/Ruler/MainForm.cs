using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ruler
{
	sealed public class MainForm : Form, IRulerInfo
	{
        private const float _ONE_CM_INCHES = 0.39370f;

        #region ResizeRegion enum

        private enum ResizeRegion
		{
			None, N, NE, E, SE, S, SW, W, NW
		}

		#endregion ResizeRegion enum

		#region Fields

		private ToolTip toolTip;
		private Point offset;
		private Rectangle mouseDownRect;
		private int resizeBorderWidth;
		private Point mouseDownFormLocation;
		private Point mouseDownPoint;
		private bool isMouseResizeCommand;
		private ResizeRegion resizeRegion;
		private List<MenuItemHolder> menuItemList;

        private int _dpiX, _dpiY;


        private bool isVertical;
		private bool isLocked;
		private bool showToolTip;

		private readonly RulerInfo initRulerInfo;

		private bool doLockRulerResizeOnMove;

		#endregion Fields

		#region Init

		[STAThread]
		private static void Main(params string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			MainForm mainForm;

			if (args.Length == 0)
			{
				mainForm = new MainForm();
			}
			else
			{
				mainForm = new MainForm(RulerInfo.CovertToRulerInfo(args));
			}

			Application.Run(mainForm);
		}

		public MainForm()
			: this(RulerInfo.GetDefaultRulerInfo())
		{
            using (var desktopHwnd = Graphics.FromHwnd(IntPtr.Zero))
            {
                var hdc = desktopHwnd.GetHdc();

                try
                {
                    _dpiX = GetDeviceCaps(hdc, (int)DeviceCap.LOGPIXELSX);
                    _dpiY = GetDeviceCaps(hdc, (int)DeviceCap.LOGPIXELSY);
                }
                finally
                {
                    desktopHwnd.ReleaseHdc(hdc);
                }
            }
        }

        public MainForm(RulerInfo rulerInfo)
		{
			this.InitializeComponent();
			this.initRulerInfo = rulerInfo;
		}

        #region WinApi methods
        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int GetDeviceCaps(IntPtr hDC, int nIndex);
        public enum DeviceCap
        {
            /// <summary>
            /// Logical pixels inch in X
            /// </summary>
            LOGPIXELSX = 88,
            /// <summary>
            /// Logical pixels inch in Y
            /// </summary>
            LOGPIXELSY = 90
        }
        #endregion

        /// <summary>
        /// Generated by Windows Forms Designer.
        /// DO NOT edit manually.
        /// </summary>
        private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
			this.SuspendLayout();
			//
			// MainForm
			//
			this.ClientSize = new System.Drawing.Size(120, 0);
			this.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			this.ClientSize = new System.Drawing.Size(0, 0);
			this.Name = "Ruler";
			this.Opacity = 0D;
			this.ResumeLayout(false);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			this.Init(this.initRulerInfo);
		}

		private void Init(RulerInfo rulerInfo)
		{
			// Set fields
			this.toolTip = new ToolTip
			{
				AutoPopDelay = 10000,
				InitialDelay = 1
			};

			this.isMouseResizeCommand = false;
			this.resizeRegion = ResizeRegion.None;
			this.resizeBorderWidth = 5;

			// Form setup ------------------
			this.SetStyle(ControlStyles.ResizeRedraw, true);
			this.UpdateStyles();

			ResourceManager resources = new ResourceManager(typeof(MainForm));
			this.Icon = (Icon)resources.GetObject("$this.Icon");
			this.Opacity = rulerInfo.Opacity;
			this.FormBorderStyle = FormBorderStyle.None;
			this.Font = new Font("Tahoma", 10);
			this.Text = "Ruler";
			this.BackColor = Color.White;

			this.TopMost = rulerInfo.TopMost;

			// Create menu
			this.CreateMenuItems(rulerInfo);

			RulerInfo.CopyInto(rulerInfo, this);
			this.doLockRulerResizeOnMove = false;

			this.SetStyle(ControlStyles.DoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
		}

		private void CreateMenuItems(RulerInfo rulerInfo)
		{
			this.ContextMenu = new ContextMenu();

			var list = new List<MenuItemHolder>()
			{
				new MenuItemHolder(MenuItemEnum.TopMost, "Stay On Top", this.TopMostHandler, rulerInfo.TopMost),
				new MenuItemHolder(MenuItemEnum.Vertical, "Vertical", this.VerticalHandler, rulerInfo.IsVertical),
				new MenuItemHolder(MenuItemEnum.ShowToolTip, "Tool Tip", this.ShowToolTipHandler, rulerInfo.ShowToolTip),
				new MenuItemHolder(MenuItemEnum.Opacity, "Opacity", null, false),
				new MenuItemHolder(MenuItemEnum.LockResize, "Lock Resizing", this.LockResizeHandler, rulerInfo.IsLocked),
				new MenuItemHolder(MenuItemEnum.SetSize, "Set size...", this.SetSizeHandler, false),
				new MenuItemHolder(MenuItemEnum.Duplicate, "Duplicate", this.DuplicateHandler, false),
				MenuItemHolder.Separator,
				new MenuItemHolder(MenuItemEnum.Reset, "Reset To Default", this.ResetToDefaulHandler, false),
				MenuItemHolder.Separator,
				new MenuItemHolder(MenuItemEnum.About, "About...", this.AboutHandler, false),
				MenuItemHolder.Separator,
#if DEBUG
				new MenuItemHolder(MenuItemEnum.RulerInfo, "Copy RulerInfo", this.CopyRulerInfo, false),
				MenuItemHolder.Separator,
#endif
				new MenuItemHolder(MenuItemEnum.Exit, "Exit", this.ExitHandler, false)
			};

			// Build opacity menu
			MenuItem opacityMenuItem = list.Find(m => m.MenuItemEnum == MenuItemEnum.Opacity).MenuItem;

			for (int i = 10; i <= 100; i += 10)
			{
				MenuItem subMenu = new MenuItem(i + "%", this.OpacityMenuHandler)
				{
					Checked = i == rulerInfo.Opacity * 100
				};
				opacityMenuItem.MenuItems.Add(subMenu);
			}

			// Build main context menu
			list.ForEach(mh => this.ContextMenu.MenuItems.Add(mh.MenuItem));

			this.menuItemList = list;
		}

		#endregion Init

		#region Properties

		public bool IsVertical
		{
			get
			{
				return this.isVertical;
			}

			set
			{
				this.isVertical = value;
				this.UpdateMenuItem(MenuItemEnum.Vertical, value);
			}
		}

		public bool IsLocked
		{
			get
			{
				return this.isLocked;
			}

			set
			{
				this.isLocked = value;
				this.UpdateMenuItem(MenuItemEnum.LockResize, value);
			}
		}

		public bool ShowToolTip
		{
			get
			{
				return this.showToolTip;
			}

			set
			{
				this.showToolTip = value;
				this.UpdateMenuItem(MenuItemEnum.ShowToolTip, value);

				if (value)
				{
					this.SetToolTip();
				}
				else
				{
					this.RemoveToolTip();
				}
			}
		}

		#endregion Properties

		#region Helpers

		private RulerInfo GetRulerInfo()
		{
			RulerInfo rulerInfo = new RulerInfo();

			RulerInfo.CopyInto(this, rulerInfo);

			return rulerInfo;
		}

		private MenuItemHolder FindMenuItem(MenuItemEnum menuItemEnum)
		{
			return this.menuItemList.Find(mih => mih.MenuItemEnum == menuItemEnum);
		}

		private void UpdateMenuItem(MenuItemEnum menuItemEnum, bool isChecked)
		{
			MenuItemHolder menuItemHolder = this.FindMenuItem(menuItemEnum);

			if (menuItemHolder != null)
			{
				menuItemHolder.MenuItem.Checked = isChecked;
			}
		}

		private void ChangeOrientation()
		{
			this.IsVertical = !this.IsVertical;
			int width = this.Width;
			this.Width = this.Height;
			this.Height = width;
		}

		private void SetToolTip()
		{
			this.toolTip.SetToolTip(this, $"Width: {Width} pixels/ {Width / (_ONE_CM_INCHES * _dpiX):0.00} cm/ {Width / (float)_dpiX:0.00} inches\nHeight: {Height} pixels/ {Height / (_ONE_CM_INCHES * _dpiY):0.00} cm/ {Height / (float)_dpiY:0.00} inches");
		}

		private void RemoveToolTip()
		{
			this.toolTip.RemoveAll();
		}

		#endregion Helpers

		#region Menu Item Handlers

#if DEBUG

		private void CopyRulerInfo(object sender, EventArgs e)
		{
			string parameters = this.GetRulerInfo().ConvertToParameters();
			Clipboard.SetText(parameters);
			MessageBox.Show(string.Concat("Copied to clipboard:", Environment.NewLine, parameters));
		}

#endif

		private void SetSizeHandler(object sender, EventArgs e)
		{
			SetSizeForm form = new SetSizeForm(this.Width, this.Height);

			if (this.TopMost)
			{
				form.TopMost = true;
			}

			if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				Size size = form.GetNewSize();

				this.Width = size.Width;
				this.Height = size.Height;
			}
		}

		private void LockResizeHandler(object sender, EventArgs e)
		{
			this.IsLocked = !this.IsLocked;
		}

		private void DuplicateHandler(object sender, EventArgs e)
		{
			string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;

			RulerInfo rulerInfo = this.GetRulerInfo();

			ProcessStartInfo startInfo = new ProcessStartInfo(exe, rulerInfo.ConvertToParameters());

			Process process = new Process
			{
				StartInfo = startInfo
			};

			process.Start();
		}

		private void OpacityMenuHandler(object sender, EventArgs e)
		{
			MenuItem opacityMenuItem = (MenuItem)sender;

			foreach (MenuItem menuItem in opacityMenuItem.Parent.MenuItems)
			{
				menuItem.Checked = false;
			}

			opacityMenuItem.Checked = true;
			this.Opacity = double.Parse(opacityMenuItem.Text.Replace("%", string.Empty)) / 100;
		}

		private void ShowToolTipHandler(object sender, EventArgs e)
		{
			this.ShowToolTip = !this.ShowToolTip;
		}

		private void ExitHandler(object sender, EventArgs e)
		{
			this.Close();
		}

		private void VerticalHandler(object sender, EventArgs e)
		{
			this.ChangeOrientation();
		}

		private void TopMostHandler(object sender, EventArgs e)
		{
			MenuItem mi = (MenuItem)sender;

			mi.Checked = !mi.Checked;
			this.TopMost = mi.Checked;
		}

		private void AboutHandler(object sender, EventArgs e)
		{
			string message = string.Format(
				"Original Ruler implemented by Jeff Key\n" +
				"www.sliver.com\n" +
				"ruler.codeplex.com\n" +
				"Icon by Kristen Magee @ www.kbecca.com.\n" +
				"Maintained by Andrija Cacanovic\n" +
				"Hosted on \n" +
				"https://github.com/andrijac/ruler\n" +
				"Version {0}",
				Application.ProductVersion);
			MessageBox.Show(message, "About Ruler", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void ResetToDefaulHandler(object sender, EventArgs e)
		{
			RulerInfo.CopyInto(RulerInfo.GetDefaultRulerInfo(), this);
		}

		#endregion Menu Item Handlers

		#region Input

		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			base.OnMouseDoubleClick(e);

			if (e.Button == MouseButtons.Left)
			{
				this.ChangeOrientation();
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			bool inResizableArea = this.GetIsInResizableArea();
			if (inResizableArea)
			{
				this.isMouseResizeCommand = true;
			}

			this.offset = new Point(Control.MousePosition.X - this.Location.X, Control.MousePosition.Y - this.Location.Y);
			this.mouseDownPoint = Control.MousePosition;
			this.mouseDownRect = this.ClientRectangle;
			this.mouseDownFormLocation = this.Location;

			this.doLockRulerResizeOnMove = !inResizableArea;

			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			this.isMouseResizeCommand = false;
			this.resizeRegion = ResizeRegion.None;
			this.doLockRulerResizeOnMove = false;

			base.OnMouseUp(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (this.resizeRegion != ResizeRegion.None && this.isMouseResizeCommand)
			{
				this.HandleResize();
				return;
			}

			Point clientCursorPos = this.PointToClient(MousePosition);

			bool inResizableArea = this.GetIsInResizableArea();

			if (inResizableArea)
			{
				ResizeRegion resizeRegion = this.GetResizeRegion(clientCursorPos);
				this.SetResizeCursor(resizeRegion);

				if (e.Button == MouseButtons.Left)
				{
					this.resizeRegion = resizeRegion;
					this.HandleResize();
				}
			}
			else
			{
				this.Cursor = Cursors.Default;

				if (e.Button == MouseButtons.Left)
				{
					this.Location = new Point(Control.MousePosition.X - this.offset.X, Control.MousePosition.Y - this.offset.Y);
				}
			}

			base.OnMouseMove(e);
		}

		protected override void OnResize(EventArgs e)
		{
			// ToolTip needs to be set again on resize to refresh new size values inside ToolTip
			if (this.ShowToolTip)
			{
				this.SetToolTip();
			}

			base.OnResize(e);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Right:
				case Keys.Left:
				case Keys.Up:
				case Keys.Down:
					this.HandleMoveResizeKeystroke(e);
					break;

				case Keys.Space:
					this.ChangeOrientation();
					break;
			}

			base.OnKeyDown(e);
		}

		private void HandleMoveResizeKeystroke(KeyEventArgs e)
		{
			int amount = e.Shift ? 1 : 5;

			if (e.KeyCode == Keys.Right)
			{
				if (e.Control)
				{
					this.Width += amount;
				}
				else
				{
					this.Left += amount;
				}
			}
			else if (e.KeyCode == Keys.Left)
			{
				if (e.Control)
				{
					this.Width -= amount;
				}
				else
				{
					this.Left -= amount;
				}
			}
			else if (e.KeyCode == Keys.Up)
			{
				if (e.Control)
				{
					this.Height -= amount;
				}
				else
				{
					this.Top -= amount;
				}
			}
			else if (e.KeyCode == Keys.Down)
			{
				if (e.Control)
				{
					this.Height += amount;
				}
				else
				{
					this.Top += amount;
				}
			}
		}

		private void HandleResize()
		{
			if (this.IsLocked || this.doLockRulerResizeOnMove)
			{
				return;
			}

			int diffX = Control.MousePosition.X - this.mouseDownPoint.X;
			int diffY = Control.MousePosition.Y - this.mouseDownPoint.Y;

			// New location and size.
			int x = 0, y = 0, width = 0, height = 0;
			BoundsSpecified bounds = BoundsSpecified.None;

			switch (this.resizeRegion)
			{
				case ResizeRegion.W:
					{
						x = this.mouseDownFormLocation.X + diffX;
						width = this.mouseDownRect.Width - diffX;
						bounds = BoundsSpecified.X | BoundsSpecified.Width;
						break;
					}

				case ResizeRegion.NW:
					{
						x = this.mouseDownFormLocation.X + diffX;
						y = this.mouseDownFormLocation.Y + diffY;
						width = this.mouseDownRect.Width - diffX;
						height = this.mouseDownRect.Height - diffY;
						bounds = BoundsSpecified.All;
						break;
					}

				case ResizeRegion.N:
					{
						y = this.mouseDownFormLocation.Y + diffY;
						height = this.mouseDownRect.Height - diffY;
						bounds = BoundsSpecified.Y | BoundsSpecified.Height;
						break;
					}

				case ResizeRegion.NE:
					{
						y = this.mouseDownFormLocation.Y + diffY;
						height = this.mouseDownRect.Height - diffY;
						width = this.mouseDownRect.Width + diffX;
						bounds = BoundsSpecified.Y | BoundsSpecified.Height | BoundsSpecified.Width;
						break;
					}

				case ResizeRegion.E:
					{
						width = this.mouseDownRect.Width + diffX;
						bounds = BoundsSpecified.Width;
						break;
					}

				case ResizeRegion.SE:
					{
						width = this.mouseDownRect.Width + diffX;
						height = this.mouseDownRect.Height + diffY;
						bounds = BoundsSpecified.Size;
						break;
					}

				case ResizeRegion.S:
					{
						height = this.mouseDownRect.Height + diffY;
						bounds = BoundsSpecified.Height;
						break;
					}

				case ResizeRegion.SW:
					{
						x = this.mouseDownFormLocation.X + diffX;
						width = this.mouseDownRect.Width - diffX;
						height = this.mouseDownRect.Height + diffY;
						bounds = BoundsSpecified.X | BoundsSpecified.Width | BoundsSpecified.Height;
						break;
					}
			}

			this.SetBounds(x, y, width, height, bounds);
		}

		private void SetResizeCursor(ResizeRegion region)
		{
			switch (region)
			{
				case ResizeRegion.N:
				case ResizeRegion.S:
					this.Cursor = Cursors.SizeNS;
					break;

				case ResizeRegion.E:
				case ResizeRegion.W:
					this.Cursor = Cursors.SizeWE;
					break;

				case ResizeRegion.NW:
				case ResizeRegion.SE:
					this.Cursor = Cursors.SizeNWSE;
					break;

				default:
					this.Cursor = Cursors.SizeNESW;
					break;
			}
		}

		private ResizeRegion GetResizeRegion(Point clientCursorPos)
		{
			if (clientCursorPos.Y <= this.resizeBorderWidth)
			{
				if (clientCursorPos.X <= this.resizeBorderWidth)
				{
					return ResizeRegion.NW;
				}
				else if (clientCursorPos.X >= this.Width - this.resizeBorderWidth)
				{
					return ResizeRegion.NE;
				}
				else
				{
					return ResizeRegion.N;
				}
			}
			else if (clientCursorPos.Y >= this.Height - this.resizeBorderWidth)
			{
				if (clientCursorPos.X <= this.resizeBorderWidth)
				{
					return ResizeRegion.SW;
				}
				else if (clientCursorPos.X >= this.Width - this.resizeBorderWidth)
				{
					return ResizeRegion.SE;
				}
				else
				{
					return ResizeRegion.S;
				}
			}
			else
			{
				if (clientCursorPos.X <= this.resizeBorderWidth)
				{
					return ResizeRegion.W;
				}
				else
				{
					return ResizeRegion.E;
				}
			}
		}

		private bool GetIsInResizableArea()
		{
			Point clientCursorPos = this.PointToClient(MousePosition);
			Rectangle resizeInnerRect = this.ClientRectangle;
			resizeInnerRect.Inflate(-this.resizeBorderWidth, -this.resizeBorderWidth);

			bool inResizableArea = this.ClientRectangle.Contains(clientCursorPos) && !resizeInnerRect.Contains(clientCursorPos);

			return inResizableArea;
		}

		#endregion Input

		#region Paint

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;

			int height = this.Height;
			int width = this.Width;
            int dpiX = _dpiX;
            int dpiY = _dpiY;


            if (this.IsVertical)
			{
				graphics.RotateTransform(90);
				graphics.TranslateTransform(0, -this.Width + 1);
				height = this.Width;
				width = this.Height;
                dpiX = _dpiY;
                dpiY = _dpiX;
            }

            DrawRuler(graphics, width, height, dpiX, dpiY, this.Font);

			base.OnPaint(e);
		}

		private static void DrawRuler(Graphics g, int formWidth, int formHeight, int dpiX, int dpiY, Font font)
		{
			// Border
			g.DrawRectangle(Pens.Black, 0, 0, formWidth - 1, formHeight - 1);

			// Width
            g.DrawString($"{formWidth/(_ONE_CM_INCHES * dpiX):0.00} cm", font, Brushes.Blue, 15, (formHeight / 2) - (font.Height / 2) - 12);
            g.DrawString($"{formWidth} pixels", font, Brushes.Red, 10, (formHeight / 2) - (font.Height / 2));
            g.DrawString($"{formWidth / (float)dpiX:0.00} in", font, Brushes.Green, 15, (formHeight / 2) - (font.Height / 2) + 13);

            g.DrawString($"[{dpiX}/{dpiY} current DPI values]", font, Brushes.DarkGray, 110, (formHeight / 2) - (font.Height / 2));

            // Ticks
            for (int i = 0; i < formWidth; i++)
			{
				if (i % 2 == 0)
				{
					int tickHeight;

					if (i % 100 == 0)
					{
						tickHeight = 15;
						DrawTickLabel(g, i.ToString(), i, formHeight, tickHeight, font);
					}
					else if (i % 10 == 0)
					{
						tickHeight = 10;
					}
					else
					{
						tickHeight = 5;
					}

					DrawTick(g, i, formHeight, tickHeight);
				}
			}
		}

		private static void DrawTick(Graphics g, int xPos, int formHeight, int tickHeight)
		{
			// Top
			g.DrawLine(Pens.Black, xPos, 0, xPos, tickHeight);

			// Bottom
			g.DrawLine(Pens.Black, xPos, formHeight, xPos, formHeight - tickHeight);
		}

		private static void DrawTickLabel(Graphics g, string text, int xPos, int formHeight, int height, Font font)
		{
			// Top
			g.DrawString(text, font, Brushes.Black, xPos, height);

			// Bottom
			g.DrawString(text, font, Brushes.Black, xPos, formHeight - height - font.Height);
		}

		#endregion Paint

		#region Diagnostics

		private static void DebugWrite(params object[] values)
		{
#if DEBUG
			string value = string.Empty;

			foreach (object o in values)
			{
				value += o == null ? "null" : o.ToString();
				value += " ";
			}

			Debug.WriteLine(value);
#endif
		}

		#endregion Diagnostics
	}
}