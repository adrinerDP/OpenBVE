using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using LibRender2;
using OpenBveApi;
using OpenBveApi.Hosts;
using OpenBveApi.Interface;
using RouteManager2;
using Path = OpenBveApi.Path;

namespace OpenBve
{
	internal partial class formMain : Form
	{
		// ===============
		// route selection
		// ===============

		// route folder
		private string rf;
		private FileSystemWatcher routeWatcher;
		private FileSystemWatcher trainWatcher;

		private Dictionary<string, string> compatibilitySignals = new Dictionary<string, string>();

		private void LoadCompatibilitySignalSets()
		{
			string[] possibleFiles = Directory.GetFiles(Path.CombineDirectory(Program.FileSystem.GetDataFolder("Compatibility"), "Signals"), "*.xml");
			for (int i = 0; i < possibleFiles.Length; i++)
			{
				XmlDocument currentXML = new XmlDocument();
				try
				{
					currentXML.Load(possibleFiles[i]);
					XmlNode node = currentXML.SelectSingleNode("/openBVE/CompatibilitySignals/SignalSetName");
					if (node != null)
					{
						compatibilitySignals.Add(node.InnerText, possibleFiles[i]);
						comboBoxCompatibilitySignals.Items.Add(node.InnerText);
					}
				}
				catch
				{
				}

			}
		}

		private void textboxRouteFolder_TextChanged(object sender, EventArgs e)
		{
			if (listviewRouteFiles.Columns.Count == 0 || OpenBveApi.Path.ContainsInvalidChars(textboxRouteFolder.Text))
			{
				return;
			}
			string Folder = textboxRouteFolder.Text;
			while (!Directory.Exists(Folder) && System.IO.Path.IsPathRooted(Folder))
			{
				try
				{
					Folder = Directory.GetParent(Folder).ToString();
				}
				catch
				{
					// Can't get the root of \\ => https://github.com/leezer3/OpenBVE/issues/468
					// Probably safer overall too
					return;
				}
				
			}

			if (rf != Folder)
			{
				populateRouteList(Folder);
			}
			rf = Folder;
			try
			{
				if (!OpenTK.Configuration.RunningOnMacOS && !String.IsNullOrEmpty(Folder) && Folder.Length > 2)
				{
					//BUG: Mono's filesystem watcher can exceed the OS-X handles limit on some systems
					//Triggered by NWM which has 600+ files in the route folder
					routeWatcher = new FileSystemWatcher();
					routeWatcher.Path = Folder;
					routeWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
					routeWatcher.Filter = "*.*";
					routeWatcher.Changed += onRouteFolderChanged;
					routeWatcher.EnableRaisingEvents = true;
				}
			}
			catch
			{
			}
			if (listviewRouteFiles.Columns.Count > 0)
			{
				listviewRouteFiles.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
			}
		}

		private void onRouteFolderChanged(object sender, EventArgs e)
		{
			//We need to invoke the control so we don't get a cross thread exception
			if (this.InvokeRequired)
			{
				this.BeginInvoke((MethodInvoker) delegate
				{
					onRouteFolderChanged(this, e);
				});
				return;
			}
			populateRouteList(rf);
			//If this method is triggered whilst the form is disposing, bad things happen...
			if (listviewRouteFiles.Columns.Count > 0)
			{
				listviewRouteFiles.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
			}
		}

		/// <summary>Populates the route display list from the selected folder</summary>
		/// <param name="Folder">The folder containing route files</param>
		private void populateRouteList(string Folder)
		{
			try
			{
				if (Folder.Length == 0)
				{
					// drives
					listviewRouteFiles.Items.Clear();
					try
					{
						// MoMA says that GetDrives is flagged with [MonoTodo]
						System.IO.DriveInfo[] driveInfos = System.IO.DriveInfo.GetDrives();
						for (int i = 0; i < driveInfos.Length; i++)
						{
							ListViewItem Item = listviewRouteFiles.Items.Add(driveInfos[i].Name);
							Item.ImageKey = Program.CurrentHost.Platform == HostPlatform.MicrosoftWindows ? @"disk" : @"folder";
							Item.Tag = driveInfos[i].RootDirectory.FullName;
							listviewRouteFiles.Tag = null;
						}
					}
					catch
					{
					}
				}
				else if (System.IO.Directory.Exists(Folder))
				{
					listviewRouteFiles.Items.Clear();
					// parent
					try
					{
						System.IO.DirectoryInfo Info = System.IO.Directory.GetParent(Folder);
						if (Info != null)
						{
							ListViewItem Item = listviewRouteFiles.Items.Add("..");
							Item.ImageKey = @"parent";
							Item.Tag = Info.FullName;
							listviewRouteFiles.Tag = Info.FullName;
						}
						else
						{
							ListViewItem Item = listviewRouteFiles.Items.Add("..");
							Item.ImageKey = @"parent";
							Item.Tag = "";
							listviewRouteFiles.Tag = "";
						}
					}
					catch
					{
					}
					// folders
					try
					{
						string[] Folders = System.IO.Directory.GetDirectories(Folder);
						Array.Sort<string>(Folders);
						for (int i = 0; i < Folders.Length; i++)
						{
							System.IO.DirectoryInfo info = new System.IO.DirectoryInfo(Folders[i]);
							if ((info.Attributes & System.IO.FileAttributes.Hidden) == 0)
							{
								string folderName = System.IO.Path.GetFileName(Folders[i]);
								if (!string.IsNullOrEmpty(folderName) && folderName[0] != '.')
								{
									ListViewItem Item = listviewRouteFiles.Items.Add(folderName);
									Item.ImageKey = @"folder";
									Item.Tag = Folders[i];
								}
							}
						}
					}
					catch
					{
					}
					// files
					try
					{
						string[] Files = System.IO.Directory.GetFiles(Folder);
						Array.Sort<string>(Files);
						for (int i = 0; i < Files.Length; i++)
						{
							if (Files[i] == null) return;
							string Extension = System.IO.Path.GetExtension(Files[i]).ToLowerInvariant();
							switch (Extension)
							{
								case ".rw":
								case ".csv":
									string fileName = System.IO.Path.GetFileName(Files[i]);
									if (!string.IsNullOrEmpty(fileName) && fileName[0] != '.')
									{
										ListViewItem Item = listviewRouteFiles.Items.Add(fileName);
										if (Extension == ".csv")
										{
											try
											{
												using (StreamReader sr = new StreamReader(Files[i], Encoding.UTF8))
												{
													string text = sr.ReadToEnd();
													if (text.IndexOf("With Track", StringComparison.OrdinalIgnoreCase) >= 0 |
													text.IndexOf("Track.", StringComparison.OrdinalIgnoreCase) >= 0 |
													text.IndexOf("$Include", StringComparison.OrdinalIgnoreCase) >= 0)
													{
														Item.ImageKey = @"route";
													}
												}
												
											}
											catch
											{
											}
										}
										else
										{
											Item.ImageKey = @"route";
										}
										Item.Tag = Files[i];
									}
									break;
							}
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}

		// route files
		private void listviewRouteFiles_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (listviewRouteFiles.SelectedItems.Count == 1)
			{
				string t;
				try
				{
					t = listviewRouteFiles.SelectedItems[0].Tag as string;
				}
				catch
				{

					return;
				}
				if (t != null)
				{

					if (System.IO.File.Exists(t))
					{
						Result.RouteFile = t;
						ShowRoute(false);
					}
					else
					{
						groupboxRouteDetails.Visible = false;
						buttonStart.Enabled = false;
					}
				}
			}
		}

		private void listviewRouteFiles_DoubleClick(object sender, EventArgs e)
		{
			if (listviewRouteFiles.SelectedItems.Count == 1)
			{
				string t = listviewRouteFiles.SelectedItems[0].Tag as string;
				if (t != null)
				{
					if (t.Length == 0 || System.IO.Directory.Exists(t))
					{
						textboxRouteFolder.Text = t;
					}
				}
			}
		}

		private void listviewRouteFiles_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Return:
					listviewRouteFiles_DoubleClick(null, null);
					break;
				case Keys.Back:
					string t = listviewRouteFiles.Tag as string;
					if (t != null)
					{
						if (t.Length == 0 || System.IO.Directory.Exists(t))
						{
							textboxRouteFolder.Text = t;
						}
					}
					break;
			}
		}

		// route recently
		private void listviewRouteRecently_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (listviewRouteRecently.SelectedItems.Count == 1)
			{
				string t = listviewRouteRecently.SelectedItems[0].Tag as string;
				if (t == null) return;
				if (!System.IO.File.Exists(t)) return;
				Result.RouteFile = t;
				ShowRoute(false);
			}
		}

		// =============
		// route details
		// =============

		// route image
		private void pictureboxRouteImage_Click(object sender, EventArgs e)
		{
			if (pictureboxRouteImage.Image != null)
			{
				formImage.ShowImageDialog(pictureboxRouteImage.Image);
			}
		}

		// route encoding
		private void comboboxRouteEncoding_SelectedIndexChanged(object sender, EventArgs e)
		{
			int i = comboboxRouteEncoding.SelectedIndex;

			if (Result.RouteFile == null && Result.ErrorFile != null)
			{
				//Workaround for the route worker thread
				Result.RouteFile = Result.ErrorFile;
			}
			if (comboboxRouteEncoding.Tag == null)
			{

				if (!(i >= 0 & i < EncodingCodepages.Length)) return;
				Result.RouteEncoding = System.Text.Encoding.GetEncoding(EncodingCodepages[i]);
				if (i == 0)
				{
					// remove from cache
					for (int j = 0; j < Interface.CurrentOptions.RouteEncodings.Length; j++)
					{
						if (Interface.CurrentOptions.RouteEncodings[j].Value == Result.RouteFile)
						{
							Interface.CurrentOptions.RouteEncodings[j] =
								Interface.CurrentOptions.RouteEncodings[Interface.CurrentOptions.RouteEncodings.Length - 1];
							Array.Resize(ref Interface.CurrentOptions.RouteEncodings,
								Interface.CurrentOptions.RouteEncodings.Length - 1);
							break;
						}
					}
				}
				else
				{
					// add to cache
					int j;
					for (j = 0; j < Interface.CurrentOptions.RouteEncodings.Length; j++)
					{
						if (Interface.CurrentOptions.RouteEncodings[j].Value == Result.RouteFile)
						{
							Interface.CurrentOptions.RouteEncodings[j].Codepage = EncodingCodepages[i];
							break;
						}
					}
					if (j == Interface.CurrentOptions.RouteEncodings.Length)
					{
						Array.Resize(ref Interface.CurrentOptions.RouteEncodings, j + 1);
						Interface.CurrentOptions.RouteEncodings[j].Codepage = EncodingCodepages[i];
						Interface.CurrentOptions.RouteEncodings[j].Value = Result.RouteFile;
					}
				}
				ShowRoute(true);
			}
		}

		private void buttonRouteEncodingLatin1_Click(object sender, EventArgs e)
		{
			for (int i = 1; i < EncodingCodepages.Length; i++)
			{
				if (EncodingCodepages[i] == 1252)
				{
					comboboxRouteEncoding.SelectedIndex = i;
					return;
				}
			}
			System.Media.SystemSounds.Hand.Play();
		}

		private void buttonRouteEncodingShiftJis_Click(object sender, EventArgs e)
		{
			for (int i = 1; i < EncodingCodepages.Length; i++)
			{
				if (EncodingCodepages[i] == 932)
				{
					comboboxRouteEncoding.SelectedIndex = i;
					return;
				}
			}
			System.Media.SystemSounds.Hand.Play();
		}

		private void buttonRouteEncodingBig5_Click(object sender, EventArgs e)
		{
			for (int i = 1; i < EncodingCodepages.Length; i++)
			{
				if (EncodingCodepages[i] == 950)
				{
					comboboxRouteEncoding.SelectedIndex = i;
					return;
				}
			}
			System.Media.SystemSounds.Hand.Play();
		}

		// ===============
		// train selection
		// ===============

		// train folder

		private string tf;

		private void textboxTrainFolder_TextChanged(object sender, EventArgs e)
		{
			if (listviewTrainFolders.Columns.Count == 0 || OpenBveApi.Path.ContainsInvalidChars(textboxTrainFolder.Text))
			{
				return;
			}
			string Folder = textboxTrainFolder.Text;
			while (!Directory.Exists(Folder) && System.IO.Path.IsPathRooted(Folder) && Folder.Length > 2)
			{
				try
				{
					Folder = Directory.GetParent(Folder).ToString();
				}
				catch
				{
					// Can't get the root of \\ => https://github.com/leezer3/OpenBVE/issues/468
					// Probably safer overall too
					return;
				}
			}
			if (tf != Folder)
			{
				populateTrainList(Folder);
			}
			tf = Folder;
			try
			{
				if (!OpenTK.Configuration.RunningOnMacOS)
				{
					trainWatcher = new FileSystemWatcher();
					trainWatcher.Path = Folder;
					trainWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
					trainWatcher.Filter = "*.*";
					trainWatcher.Changed += onTrainFolderChanged;
					trainWatcher.EnableRaisingEvents = true;
				}
			}
			catch
			{
			}
			listviewTrainFolders.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
		}

		private void onTrainFolderChanged(object sender, EventArgs e)
		{
			//We need to invoke the control so we don't get a cross thread exception
			if (this.InvokeRequired)
			{
				this.BeginInvoke((MethodInvoker) delegate
				{
					onTrainFolderChanged(this, e);
				});
				return;
			}
			populateTrainList(tf);
			if (listviewTrainFolders.Columns.Count > 0)
			{
				listviewTrainFolders.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
			}
		}

		/// <summary>Populates the train display list from the selected folder</summary>
		/// <param name="Folder">The folder containing train folders</param>
		private void populateTrainList(string Folder)
		{
			try
			{
				if (Folder.Length == 0)
				{
					// drives
					listviewTrainFolders.Items.Clear();
					try
					{
						// MoMA says that GetDrives is flagged with [MonoTodo]
						System.IO.DriveInfo[] driveInfos = System.IO.DriveInfo.GetDrives();
						for (int i = 0; i < driveInfos.Length; i++)
						{
							ListViewItem Item = listviewTrainFolders.Items.Add(driveInfos[i].Name);
							Item.ImageKey = Program.CurrentHost.Platform == HostPlatform.MicrosoftWindows ? @"disk" : @"folder";
							
							Item.Tag = driveInfos[i].RootDirectory.FullName;
							listviewTrainFolders.Tag = null;
						}
					}
					catch
					{
					}
				}
				else if (System.IO.Directory.Exists(Folder))
				{
					listviewTrainFolders.Items.Clear();
					// parent
					try
					{
						System.IO.DirectoryInfo Info = System.IO.Directory.GetParent(Folder);
						if (Info != null)
						{
							ListViewItem Item = listviewTrainFolders.Items.Add("..");
							Item.ImageKey = @"parent";
							Item.Tag = Info.FullName;
							listviewTrainFolders.Tag = Info.FullName;
						}
						else
						{
							ListViewItem Item = listviewTrainFolders.Items.Add("..");
							Item.ImageKey = @"parent";
							Item.Tag = "";
							listviewTrainFolders.Tag = "";
						}
					}
					catch
					{
					}
					// folders
					try
					{
						string[] Folders = System.IO.Directory.GetDirectories(Folder);
						Array.Sort<string>(Folders);
						for (int i = 0; i < Folders.Length; i++)
						{
							try
							{
								System.IO.DirectoryInfo info = new System.IO.DirectoryInfo(Folders[i]);
								if ((info.Attributes & System.IO.FileAttributes.Hidden) == 0)
								{
									string folderName = System.IO.Path.GetFileName(Folders[i]);
									if (!string.IsNullOrEmpty(folderName) && folderName[0] != '.')
									{
										string File = OpenBveApi.Path.CombineFile(Folders[i], "train.dat");
										ListViewItem Item = listviewTrainFolders.Items.Add(folderName);
										Item.ImageKey = System.IO.File.Exists(File) ? "train" : "folder";
										Item.Tag = Folders[i];
									}
								}
							}
							catch
							{
							}
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}

		// train folders
		private void listviewTrainFolders_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (listviewTrainFolders.SelectedItems.Count == 1)
			{
				string t;
				try
				{
					t = listviewTrainFolders.SelectedItems[0].Tag as string;
				}
				catch (Exception)
				{
					return;
				}
				if (t != null) {
					if (System.IO.Directory.Exists(t)) {
						string File = OpenBveApi.Path.CombineFile(t, "train.dat");
						if (System.IO.File.Exists(File)) {
							Result.TrainFolder = t;
							ShowTrain(false);
							if (checkboxTrainDefault.Checked) checkboxTrainDefault.Checked = false;
						}
						else
						{
							groupboxTrainDetails.Visible = false;
							buttonStart.Enabled = false;
						}
					}
				}
			}
		}
		private void listviewTrainFolders_DoubleClick(object sender, EventArgs e) {
			if (listviewTrainFolders.SelectedItems.Count == 1) {
				string t = listviewTrainFolders.SelectedItems[0].Tag as string;
				if (t != null) {
					if (t.Length == 0 || System.IO.Directory.Exists(t)) {
						textboxTrainFolder.Text = t;
					}
				}
			}
		}
		private void listviewTrainFolders_KeyDown(object sender, KeyEventArgs e) {
			switch (e.KeyCode) {
				case Keys.Return:
					listviewTrainFolders_DoubleClick(null, null);
					break;
				case Keys.Back:
					string t = listviewTrainFolders.Tag as string;
					if (t != null) {
						if (t.Length == 0 || System.IO.Directory.Exists(t)) {
							textboxTrainFolder.Text = t;
						}
					} break;
			}
		}

		// train recently
		private void listviewTrainRecently_SelectedIndexChanged(object sender, EventArgs e) {
			if (listviewTrainRecently.SelectedItems.Count == 1) {
				string t = listviewTrainRecently.SelectedItems[0].Tag as string;
				if (t != null) {
					if (System.IO.Directory.Exists(t)) {
						string File = OpenBveApi.Path.CombineFile(t, "train.dat");
						if (System.IO.File.Exists(File)) {
							Result.TrainFolder = t;
							ShowTrain(false);
							if (checkboxTrainDefault.Checked) checkboxTrainDefault.Checked = false;
						}
					}
				}
			}
		}

		// train default
		void CheckboxTrainDefaultCheckedChanged(object sender, System.EventArgs e) {
			if (checkboxTrainDefault.Checked) {
				if (listviewTrainFolders.SelectedItems.Count == 1) {
					listviewTrainFolders.SelectedItems[0].Selected = false;
				}
				if (listviewTrainRecently.SelectedItems.Count == 1) {
					listviewTrainRecently.SelectedItems[0].Selected = false;
				}
				ShowDefaultTrain();
			}
		}
		

		// =============
		// train details
		// =============

		// train image
		private void pictureboxTrainImage_Click(object sender, EventArgs e) {
			if (pictureboxTrainImage.Image != null) {
				formImage.ShowImageDialog(pictureboxTrainImage.Image);
			}
		}

		// train encoding
		private void comboboxTrainEncoding_SelectedIndexChanged(object sender, EventArgs e) {
			if (comboboxTrainEncoding.Tag == null) {
				int i = comboboxTrainEncoding.SelectedIndex;
				if (i >= 0 & i < EncodingCodepages.Length) {
					Result.TrainEncoding = System.Text.Encoding.GetEncoding(EncodingCodepages[i]);
					if (i == 0) {
						// remove from cache
						for (int j = 0; j < Interface.CurrentOptions.TrainEncodings.Length; j++) {
							if (Interface.CurrentOptions.TrainEncodings[j].Value == Result.TrainFolder) {
								Interface.CurrentOptions.TrainEncodings[j] = Interface.CurrentOptions.TrainEncodings[Interface.CurrentOptions.TrainEncodings.Length - 1];
								Array.Resize(ref Interface.CurrentOptions.TrainEncodings, Interface.CurrentOptions.TrainEncodings.Length - 1);
								break;
							}
						}
					} else {
						// add to cache
						int j; for (j = 0; j < Interface.CurrentOptions.TrainEncodings.Length; j++) {
							if (Interface.CurrentOptions.TrainEncodings[j].Value == Result.TrainFolder) {
								Interface.CurrentOptions.TrainEncodings[j].Codepage = EncodingCodepages[i];
								break;
							}
						} if (j == Interface.CurrentOptions.TrainEncodings.Length) {
							Array.Resize(ref Interface.CurrentOptions.TrainEncodings, j + 1);
							Interface.CurrentOptions.TrainEncodings[j].Codepage = EncodingCodepages[i];
							Interface.CurrentOptions.TrainEncodings[j].Value = Result.TrainFolder;
						}
					}
					ShowTrain(true);
				}
			}
		}
		private void buttonTrainEncodingLatin1_Click(object sender, EventArgs e) {
			for (int i = 1; i < EncodingCodepages.Length; i++) {
				if (EncodingCodepages[i] == 1252) {
					comboboxTrainEncoding.SelectedIndex = i;
					return;
				}
			}
			System.Media.SystemSounds.Hand.Play();
		}
		private void buttonTrainEncodingShiftJis_Click(object sender, EventArgs e) {
			for (int i = 1; i < EncodingCodepages.Length; i++) {
				if (EncodingCodepages[i] == 932) {
					comboboxTrainEncoding.SelectedIndex = i;
					return;
				}
			}
			System.Media.SystemSounds.Hand.Play();
		}
		private void buttonTrainEncodingBig5_Click(object sender, EventArgs e) {
			for (int i = 1; i < EncodingCodepages.Length; i++) {
				if (EncodingCodepages[i] == 950) {
					comboboxTrainEncoding.SelectedIndex = i;
					return;
				}
			}
			System.Media.SystemSounds.Hand.Play();
		}

		
		// =====
		// start
		// =====

		// start
		private readonly object StartGame = new Object();

		private void buttonStart_Click(object sender, EventArgs e) {
			if (Result.RouteFile != null & Result.TrainFolder != null) {
				if (System.IO.File.Exists(Result.RouteFile) & System.IO.Directory.Exists(Result.TrainFolder)) {
					Result.Start = true;
					buttonClose_Click(StartGame, e);
					//HACK: Call Application.DoEvents() to force the message pump to process all pending messages when the form closes
					//This fixes the main form failing to close on Linux
					Application.DoEvents();
				}
			} else {
				System.Media.SystemSounds.Exclamation.Play();
			}
		}
		
		
		// =========
		// functions
		// =========

		private BackgroundWorker routeWorkerThread;

		private void routeWorkerThread_doWork(object sender, DoWorkEventArgs e)
		{
			if (string.IsNullOrEmpty(Result.RouteFile))
			{
				return;
			}

			if (!Plugins.LoadPlugins())
			{
				throw new Exception("Unable to load the required plugins- Please reinstall OpenBVE");
			}
			Game.Reset(false);
			bool loaded = false;
			for (int i = 0; i < Program.CurrentHost.Plugins.Length; i++)
			{
				if (Program.CurrentHost.Plugins[i].Route != null && Program.CurrentHost.Plugins[i].Route.CanLoadRoute(Result.RouteFile))
				{
					object Route = (object)Program.CurrentRoute; //must cast to allow us to use the ref keyword.
					string RailwayFolder = Loading.GetRailwayFolder(Result.RouteFile);
					string ObjectFolder = OpenBveApi.Path.CombineDirectory(RailwayFolder, "Object");
					string SoundFolder = OpenBveApi.Path.CombineDirectory(RailwayFolder, "Sound");
					if (Program.CurrentHost.Plugins[i].Route.LoadRoute(Result.RouteFile, Result.RouteEncoding, null, ObjectFolder, SoundFolder, true, ref Route))
					{
						Program.CurrentRoute = (CurrentRoute) Route;
					}
					else
					{
						if (Program.CurrentHost.Plugins[i].Route.LastException != null)
						{
							throw Program.CurrentHost.Plugins[i].Route.LastException; //Re-throw last exception generated by the route parser plugin so that the UI thread captures it
						}
						else
						{
							throw new Exception("An unknown error was enountered whilst attempting to parser the routefile " + Result.RouteFile);
						}
					}
					loaded = true;
					break;
				}
			}

			if (Loading.Complete)
			{
				Plugins.UnloadPlugins();
			}
			
			if (!loaded)
			{
				throw new Exception("No plugins capable of loading routefile " + Result.RouteFile + " were found.");
			}

		}

		private void routeWorkerThread_completed(object sender, RunWorkerCompletedEventArgs e)
		{
			if (e.Error != null || Program.CurrentRoute == null)
			{
				TryLoadImage(pictureboxRouteImage, "route_error.png");
				if (e.Error != null)
				{
					textboxRouteDescription.Text = e.Error.Message;
				}
				textboxRouteEncodingPreview.Text = "";
				pictureboxRouteMap.Image = null;
				pictureboxRouteGradient.Image = null;
				Result.ErrorFile = Result.RouteFile;
				Result.RouteFile = null;
				checkboxTrainDefault.Text = Translations.GetInterfaceString("start_train_usedefault");
				routeWorkerThread.Dispose();
				this.Cursor = System.Windows.Forms.Cursors.Default;
				return;
			}
			try
			{
				lock (BaseRenderer.GdiPlusLock)
				{
					pictureboxRouteMap.Image = Illustrations.CreateRouteMap(pictureboxRouteMap.Width, pictureboxRouteMap.Height, false);
					pictureboxRouteGradient.Image = Illustrations.CreateRouteGradientProfile(pictureboxRouteGradient.Width,
						pictureboxRouteGradient.Height, false);
				}
				// image
				if (!string.IsNullOrEmpty(Program.CurrentRoute.Image))
				{
					TryLoadImage(pictureboxRouteImage, Program.CurrentRoute.Image);
				}
				else
				{
					string[] f = {".png", ".bmp", ".gif", ".tiff", ".tif", ".jpeg", ".jpg"};
					int i;
					for (i = 0; i < f.Length; i++)
					{
						string g = OpenBveApi.Path.CombineFile(System.IO.Path.GetDirectoryName(Result.RouteFile),
							System.IO.Path.GetFileNameWithoutExtension(Result.RouteFile) + f[i]);
						if (System.IO.File.Exists(g))
						{
							try
							{
								using (var fs = new FileStream(g, FileMode.Open, FileAccess.Read))
								{
									pictureboxRouteImage.Image = new Bitmap(fs);
								}
							}
							catch
							{
								pictureboxRouteImage.Image = null;
							}
							break;
						}
					}
					if (i == f.Length)
					{
						TryLoadImage(pictureboxRouteImage, "route_unknown.png");
					}
				}

				// description
				string Description = Program.CurrentRoute.Comment.ConvertNewlinesToCrLf();
				if (Description.Length != 0)
				{
					textboxRouteDescription.Text = Description;
				}
				else
				{
					textboxRouteDescription.Text = System.IO.Path.GetFileNameWithoutExtension(Result.RouteFile);
				}

				textboxRouteEncodingPreview.Text = Description.ConvertNewlinesToCrLf();
				if (Interface.CurrentOptions.TrainName != null)
				{
					checkboxTrainDefault.Text = Translations.GetInterfaceString("start_train_usedefault") + @" (" + Interface.CurrentOptions.TrainName + @")";
				}
				else
				{
					checkboxTrainDefault.Text = Translations.GetInterfaceString("start_train_usedefault");
				}
				Result.ErrorFile = null;
			}
			catch (Exception ex)
			{
				TryLoadImage(pictureboxRouteImage, "route_error.png");
				textboxRouteDescription.Text = ex.Message;
				textboxRouteEncodingPreview.Text = "";
				pictureboxRouteMap.Image = null;
				pictureboxRouteGradient.Image = null;
				Result.ErrorFile = Result.RouteFile;
				Result.RouteFile = null;
				checkboxTrainDefault.Text = Translations.GetInterfaceString("start_train_usedefault");
			}
			

			if (checkboxTrainDefault.Checked)
			{
				ShowDefaultTrain();
			}

			this.Cursor = System.Windows.Forms.Cursors.Default;
			//Deliberately select the tab when the process is complete
			//This hopefully fixes another instance of the 'grey tabs' bug
			
			tabcontrolRouteDetails.SelectedTab = tabpageRouteDescription;

			buttonStart.Enabled = Result.RouteFile != null & Result.TrainFolder != null;
		}


		// show route
		private void ShowRoute(bool UserSelectedEncoding) {
			if (routeWorkerThread == null)
			{
				return;
			}
			if (Result.RouteFile != null && !routeWorkerThread.IsBusy)
			{
				this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
				TryLoadImage(pictureboxRouteImage, "loading.png");
				groupboxRouteDetails.Visible = true;
				textboxRouteDescription.Text = Translations.GetInterfaceString("start_route_processing");

				// determine encoding
				if (!UserSelectedEncoding) {
					Result.RouteEncoding = TextEncoding.GetSystemEncodingFromFile(Result.RouteFile);
					comboboxRouteEncoding.Tag = new object();
					comboboxRouteEncoding.SelectedIndex = 0;
					comboboxRouteEncoding.Items[0] = $"{Result.RouteEncoding.EncodingName} - {Result.RouteEncoding.CodePage}";
					comboboxRouteEncoding.Tag = null;

					comboboxRouteEncoding.Tag = new object();
					int i;
					for (i = 0; i < Interface.CurrentOptions.RouteEncodings.Length; i++) {
						if (Interface.CurrentOptions.RouteEncodings[i].Value == Result.RouteFile) {
							int j;
							for (j = 1; j < EncodingCodepages.Length; j++) {
								if (EncodingCodepages[j] == Interface.CurrentOptions.RouteEncodings[i].Codepage) {
									comboboxRouteEncoding.SelectedIndex = j;
									Result.RouteEncoding = System.Text.Encoding.GetEncoding(EncodingCodepages[j]);
									break;
								}
							}
							if (j == EncodingCodepages.Length) {
								comboboxRouteEncoding.SelectedIndex = 0;
								Result.RouteEncoding = System.Text.Encoding.UTF8;
							}
							break;
						}
					}
					comboboxRouteEncoding.Tag = null;
				}
				if (!routeWorkerThread.IsBusy)
				{
					//HACK: If clicking very rapidly or holding down an arrow
					//		we can sometimes try to spawn two worker threads
					routeWorkerThread.RunWorkerAsync();
				}
			}
		}

		// show train
		private void ShowTrain(bool UserSelectedEncoding) {
			if (!UserSelectedEncoding) {
				Result.TrainEncoding = TextEncoding.GetSystemEncodingFromFile(Result.TrainFolder, "train.txt");
				comboboxTrainEncoding.Tag = new object();
				comboboxTrainEncoding.SelectedIndex = 0;
				comboboxTrainEncoding.Items[0] = $"{Result.TrainEncoding.EncodingName} - {Result.TrainEncoding.CodePage}";

				comboboxTrainEncoding.Tag = null;
				int i;
				for (i = 0; i < Interface.CurrentOptions.TrainEncodings.Length; i++) {
					if (Interface.CurrentOptions.TrainEncodings[i].Value == Result.TrainFolder) {
						int j;
						for (j = 1; j < EncodingCodepages.Length; j++) {
							if (EncodingCodepages[j] == Interface.CurrentOptions.TrainEncodings[i].Codepage) {
								comboboxTrainEncoding.SelectedIndex = j;
								Result.TrainEncoding = System.Text.Encoding.GetEncoding(EncodingCodepages[j]);
								break;
							}
						}
						if (j == EncodingCodepages.Length) {
							comboboxTrainEncoding.SelectedIndex = 0;
							Result.TrainEncoding = System.Text.Encoding.UTF8;
						}
						break;
					}
				}
				panelTrainEncoding.Enabled = true;
				comboboxTrainEncoding.Tag = null;
			}
			{
				// train image
				string File = OpenBveApi.Path.CombineFile(Result.TrainFolder, "train.png");
				if (!System.IO.File.Exists(File)) {
					File = OpenBveApi.Path.CombineFile(Result.TrainFolder, "train.bmp");
				}
				if (System.IO.File.Exists(File)) {
					TryLoadImage(pictureboxTrainImage, File);
				} else {
					TryLoadImage(pictureboxTrainImage, "train_unknown.png");
				}
			}
			{
				// train description
				string File = OpenBveApi.Path.CombineFile(Result.TrainFolder, "train.txt");
				if (System.IO.File.Exists(File)) {
					try {
						string trainText = System.IO.File.ReadAllText(File, Result.TrainEncoding);
						trainText = trainText.ConvertNewlinesToCrLf();
						textboxTrainDescription.Text = trainText;
						textboxTrainEncodingPreview.Text = trainText;
					} catch {
						textboxTrainDescription.Text = System.IO.Path.GetFileName(Result.TrainFolder);
						textboxTrainEncodingPreview.Text = "";
					}
				} else {
					textboxTrainDescription.Text = System.IO.Path.GetFileName(Result.TrainFolder);
					textboxTrainEncodingPreview.Text = "";
				}
			}
			groupboxTrainDetails.Visible = true;
			labelTrainEncoding.Enabled = true;
			labelTrainEncodingPreview.Enabled = true;
			textboxTrainEncodingPreview.Enabled = true;
			buttonStart.Enabled = Result.RouteFile != null & Result.TrainFolder != null;
		}

		// show default train
		private void ShowDefaultTrain() {
			
			if (string.IsNullOrEmpty(Result.RouteFile)) {
				return;
			}
			if (string.IsNullOrEmpty(Interface.CurrentOptions.TrainName)) {
				return;
			}
			
			string Folder;
			try {
				Folder = System.IO.Path.GetDirectoryName(Result.RouteFile);
				if (Interface.CurrentOptions.TrainName[0] == '$') {
					Folder = OpenBveApi.Path.CombineDirectory(Folder, Interface.CurrentOptions.TrainName);
					if (System.IO.Directory.Exists(Folder)) {
						string File = OpenBveApi.Path.CombineFile(Folder, "train.dat");
						if (System.IO.File.Exists(File)) {
							
							Result.TrainFolder = Folder;
							ShowTrain(false);
							return;
						}
					}
				}
			} catch {
				Folder = null;
			}
			bool recursionTest = false;
			string lastFolder = null;
			try {
				while (true) {
					string TrainFolder = OpenBveApi.Path.CombineDirectory(Folder, "Train");
					var OldFolder = Folder;
					if (System.IO.Directory.Exists(TrainFolder)) {
						try {
							Folder = OpenBveApi.Path.CombineDirectory(TrainFolder, Interface.CurrentOptions.TrainName);
						} catch (Exception ex) {
							if (ex is ArgumentException)
							{
								break; // Invalid character in path causes infinite recursion
							}
							Folder = null;
						}
						if (Folder != null) {
							char c = System.IO.Path.DirectorySeparatorChar;
							if (System.IO.Directory.Exists(Folder))
							{

								string File = OpenBveApi.Path.CombineFile(Folder, "train.dat");
								if (System.IO.File.Exists(File))
								{
									// train found
									Result.TrainFolder = Folder;
									ShowTrain(false);
									return;
								}
								if (lastFolder == Folder || recursionTest)
								{
									break;
								}
								lastFolder = Folder;						
							}
							else if (Folder.ToLowerInvariant().Contains(c + "railway" + c))
							{
								//If we have a misplaced Train folder in either our Railway\Route
								//or Railway folders, this can cause the train search to fail
								//Detect the presence of a railway folder and carry on traversing upwards if this is the case
								recursionTest = true;
								Folder = OldFolder;
							}
							else
							{
								break;
							}
						}
					}
					if (Folder == null) continue;
					System.IO.DirectoryInfo Info = System.IO.Directory.GetParent(Folder);
					if (Info != null) {
						Folder = Info.FullName;
					} else {
						break;
					}
				}
			} catch { }
			// train not found
			Result.TrainFolder = null;
			TryLoadImage(pictureboxTrainImage, "train_error.png");
			textboxTrainDescription.Text = (Translations.GetInterfaceString("start_train_notfound") + Interface.CurrentOptions.TrainName).ConvertNewlinesToCrLf();
			comboboxTrainEncoding.Tag = new object();
			comboboxTrainEncoding.SelectedIndex = 0;
			comboboxTrainEncoding.Tag = null;
			labelTrainEncoding.Enabled = false;
			panelTrainEncoding.Enabled = false;
			labelTrainEncodingPreview.Enabled = false;
			textboxTrainEncodingPreview.Enabled = false;
			textboxTrainEncodingPreview.Text = "";
			groupboxTrainDetails.Visible = true;
		}

	}
}
