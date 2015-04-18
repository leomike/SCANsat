﻿#region license
/* 
 *  [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 *
 * SCANsat - Zoom window object
 * 
 * Copyright (c)2013 damny;
 * Copyright (c)2014 David Grandy <david.grandy@gmail.com>;
 * Copyright (c)2014 technogeeky <technogeeky@gmail.com>;
 * Copyright (c)2014 (Your Name Here) <your email here>; see LICENSE.txt for licensing details.
 *
 */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using SCANsat.SCAN_Platform;
using SCANsat;
using SCANsat.SCAN_UI.UI_Framework;
using SCANsat.SCAN_Data;
using SCANsat.SCAN_Map;
using palette = SCANsat.SCAN_UI.UI_Framework.SCANpalette;
using UnityEngine;

namespace SCANsat.SCAN_UI
{
	class SCANzoomWindow : SCAN_MBW
	{
		private SCANmap spotmap;
		private SCANmap bigmap;
		private CelestialBody b;
		private SCANdata data;
		private Vessel v;
		private bool showOrbit, showAnomaly, showWaypoints, showInfo, controlLock;
		private Vector2 dragStart;
		private Vector2d mjTarget = new Vector2d();
		private float resizeW, resizeH;
		private const string lockID = "SCANzoom_LOCK";
		internal readonly static Rect defaultRect = new Rect(50f, 50f, 340f, 240f);
		private static Rect sessionRect = defaultRect;

		protected override void Awake()
		{
			WindowRect = sessionRect;
			WindowSize_Min = new Vector2(310, 180);
			WindowSize_Max = new Vector2(540, 400);
			WindowOptions = new GUILayoutOption[2] { GUILayout.Width(340), GUILayout.Height(240) };
			WindowStyle = SCANskins.SCAN_window;
			showInfo = true;
			Visible = false;
			DragEnabled = true;
			ClampEnabled = true;
			TooltipMouseOffset = new Vector2d(-10, -25);
			ClampToScreenOffset = new RectOffset(-200, -200, -160, -160);

			SCAN_SkinsLibrary.SetCurrent("SCAN_Unity");
			SCAN_SkinsLibrary.SetCurrentTooltip();

			removeControlLocks();

			Startup();
		}

		private void Startup()
		{
			//Initialize the map object
			Visible = false;
			if (HighLogic.LoadedSceneIsFlight)
			{
				v = SCANcontroller.controller.BigMap.V;
				b = SCANcontroller.controller.BigMap.Body;
				data = SCANcontroller.controller.BigMap.Data;
			}
			else if (HighLogic.LoadedSceneHasPlanetarium)
			{
				v = null;
				b = SCANcontroller.controller.kscMap.Body;
				data = SCANcontroller.controller.kscMap.Data;
			}
			if (spotmap == null)
			{
				spotmap = new SCANmap();
				spotmap.setSize(320, 240);
			}

			showOrbit = SCANcontroller.controller.map_orbit;
			showAnomaly = SCANcontroller.controller.map_markers;

			if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
				showWaypoints = false;
			else
				showWaypoints = SCANcontroller.controller.map_waypoints;

			TooltipsEnabled = SCANcontroller.controller.toolTips;

			spotmap.setBody(b);
		}

		protected override void OnDestroy()
		{
			removeControlLocks();
		}

		internal void removeControlLocks()
		{
			InputLockManager.RemoveControlLock(lockID);
			controlLock = false;
		}

		public void setMapCenter(double lat, double lon, SCANmap big)
		{
			Visible = true;
			bigmap = big;

			SCANdata dat = SCANUtil.getData(bigmap.Body);
			if (dat == null)
				dat = new SCANdata(bigmap.Body);

			data = dat;
			b = data.Body;

			spotmap.MapScale = 10;
			spotmap.setBody(b);

			if (bigmap.Projection == MapProjection.Polar)
				spotmap.setProjection(MapProjection.Polar);
			else
				spotmap.setProjection(MapProjection.Rectangular);

			spotmap.centerAround(lon, lat);
			spotmap.resetMap(bigmap.MType, false);
		}

		private void resetMap()
		{
			SCANcontroller.controller.MechJebSelecting = false;
			SCANcontroller.controller.MechJebSelectingActive = false;
			spotmap.centerAround(spotmap.CenteredLong, spotmap.CenteredLat);
			spotmap.resetMap();
		}

		public SCANmap SpotMap
		{
			get { return spotmap; }
		}

		protected override void DrawWindowPre(int id)
		{
			WindowCaption = SCANuiUtil.toDMS(spotmap.CenteredLat, spotmap.CenteredLong);

			if (IsResizing && !inRepaint())
			{
				if (Input.GetMouseButtonUp(0))
				{
					double scale = spotmap.MapScale;
					IsResizing = false;
					if (resizeW < WindowSize_Min.x)
						resizeW = WindowSize_Min.x;
					else if (resizeW > WindowSize_Max.x)
						resizeW = WindowSize_Max.x;
					if (resizeH < WindowSize_Min.y)
						resizeH = WindowSize_Min.y;
					else if (resizeH > WindowSize_Max.y)
						resizeH = WindowSize_Max.y;

					spotmap.setSize((int)resizeW, (int)resizeH);
					spotmap.MapScale = scale;
					spotmap.centerAround(spotmap.CenteredLong, spotmap.CenteredLat);
					spotmap.resetMap(spotmap.MType, false);
				}
				else
				{
					float yy = Input.mousePosition.y;
					float xx = Input.mousePosition.x;
					if (Input.mousePosition.y < 0)
						yy = 0;
					if (Input.mousePosition.x < 0)
						xx = 0;

					resizeH += dragStart.y - yy;
					dragStart.y = yy;
					resizeW += xx - dragStart.x;
					dragStart.x = xx;
				}
				if (Event.current.isMouse)
					Event.current.Use();
			}

			//Lock space center click through
			if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
			{
				Vector2 mousePos = Input.mousePosition;
				mousePos.y = Screen.height - mousePos.y;
				if (WindowRect.Contains(mousePos) && !controlLock)
				{
					InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS | ControlTypes.KSC_ALL, lockID);
					controlLock = true;
				}
				else if (!WindowRect.Contains(mousePos) && controlLock)
				{
					InputLockManager.RemoveControlLock(lockID);
					controlLock = false;
				}
			}

			//Lock tracking scene click through
			if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
			{
				Vector2 mousePos = Input.mousePosition;
				mousePos.y = Screen.height - mousePos.y;
				if (WindowRect.Contains(mousePos) && !controlLock)
				{
					InputLockManager.SetControlLock(ControlTypes.TRACKINGSTATION_UI, lockID);
					controlLock = true;
				}
				else if (!WindowRect.Contains(mousePos) && controlLock)
				{
					InputLockManager.RemoveControlLock(lockID);
					controlLock = false;
				}
			}
		}

		protected override void DrawWindow(int id)
		{
			versionLabel(id);
			closeBox(id);

			growS();
				topBar(id);
				drawMap(id);
				mouseOver(id);
			stopS();

			mapLabels(id);
		}

		protected override void DrawWindowPost(int id)
		{
			sessionRect = WindowRect;

			if (SCANcontroller.controller.MechJebSelecting && Event.current.type == EventType.mouseDown && !TextureRect.Contains(Event.current.mousePosition))
			{
				SCANcontroller.controller.MechJebSelecting = false;
				SCANcontroller.controller.MechJebSelectingActive = false;
			}
		}

		//Draw version label in upper left corner
		private void versionLabel(int id)
		{
			Rect r = new Rect(6, 0, 50, 18);
			GUI.Label(r, SCANmainMenuLoader.SCANsatVersion, SCANskins.SCAN_whiteReadoutLabel);
		}

		//Draw the close button in upper right corner
		private void closeBox(int id)
		{
			Rect r = new Rect(WindowRect.width - 40, 0, 18, 18);
			if (showInfo)
			{
				if (GUI.Button(r, "-", SCANskins.SCAN_buttonBorderless))
					showInfo = !showInfo;
			}
			else
			{
				if (GUI.Button(r, "+", SCANskins.SCAN_buttonBorderless))
					showInfo = !showInfo;
			}
			r.x += 20;
			r.y += 1;
			if (GUI.Button(r, SCANcontroller.controller.closeBox, SCANskins.SCAN_closeButton))
			{
				removeControlLocks();
				Visible = false;
			}
		}

		private void topBar(int id)
		{
			growE();
			showOrbit = GUILayout.Toggle(showOrbit, textWithTT("", "Toggle Orbit"));

			Rect d = GUILayoutUtility.GetLastRect();
			d.x += 30;
			d.y += 2;
			d.width = 40;
			d.height = 20;

			if (GUI.Button(d, iconWithTT(SCANskins.SCAN_OrbitIcon, "Toggle Orbit"), SCANskins.SCAN_buttonBorderless))
			{
				showOrbit = !showOrbit;
			}

			if (SCANcontroller.controller.MechJebLoaded && SCANcontroller.controller.MechJebTargetBody == b)
			{
				fillS(50);
				if (GUILayout.Button(iconWithTT(SCANskins.SCAN_MechJebIcon, "Set MechJeb Target"), SCANskins.SCAN_buttonBorderless, GUILayout.Width(24), GUILayout.Height(24)))
				{
					SCANcontroller.controller.MechJebSelecting = !SCANcontroller.controller.MechJebSelecting;
				}
			}
			else
				GUILayout.Label("", GUILayout.Width(70));

			fillS();

			if (GUILayout.Button(iconWithTT(SCANskins.SCAN_ZoomOutIcon, "Zoom Out"), SCANskins.SCAN_buttonBorderless, GUILayout.Width(26), GUILayout.Height(26)))
			{
				spotmap.MapScale = spotmap.MapScale / 1.25f;
				if (spotmap.MapScale < 2)
					spotmap.MapScale = 2;
				resetMap();
			}

			if (GUILayout.Button(textWithTT(spotmap.MapScale.ToString("N1") + " X", "Sync To Big Map"), SCANskins.SCAN_buttonBorderless, GUILayout.Width(50), GUILayout.Height(24)))
			{
				SCANcontroller.controller.MechJebSelecting = false;
				SCANcontroller.controller.MechJebSelectingActive = false;

				if (bigmap.Projection == MapProjection.Polar)
					spotmap.setProjection(MapProjection.Polar);
				else
					spotmap.setProjection(MapProjection.Rectangular);

				if (bigmap.Body != b)
				{
					SCANdata dat = SCANUtil.getData(bigmap.Body);
					if (dat == null)
						dat = new SCANdata(bigmap.Body);

					data = dat;
					b = data.Body;

					spotmap.setBody(b);
				}

				if (SCANconfigLoader.GlobalResource)
				{
					spotmap.Resource = bigmap.Resource;
					spotmap.Resource.CurrentBodyConfig(b.name);
				}

				spotmap.centerAround(spotmap.CenteredLong, spotmap.CenteredLat);

				spotmap.resetMap(bigmap.MType, false);
			}

			if (GUILayout.Button(iconWithTT(SCANskins.SCAN_ZoomInIcon, "Zoom In"), SCANskins.SCAN_buttonBorderless, GUILayout.Width(26), GUILayout.Height(26)))
			{
				spotmap.MapScale = spotmap.MapScale * 1.25f;
				resetMap();
			}

			fillS();

			if (HighLogic.LoadedScene != GameScenes.SPACECENTER)
			{
				showWaypoints = GUILayout.Toggle(showWaypoints, textWithTT("", "Toggle Waypoints"));

				d = GUILayoutUtility.GetLastRect();
				d.x += 28;
				d.y += 2;
				d.width = 20;
				d.height = 20;

				if (GUI.Button(d, iconWithTT(SCANskins.SCAN_WaypointIcon, "Toggle Waypoints"), SCANskins.SCAN_buttonBorderless))
				{
					showWaypoints = !showWaypoints;
				}

				fillS(16);
			}
			else
				GUILayout.Label("", GUILayout.Width(60));

			showAnomaly = GUILayout.Toggle(showAnomaly, textWithTT("", "Toggle Anomalies"));

			d = GUILayoutUtility.GetLastRect();
			d.x += 26;
			d.y += 2;
			d.width = 20;
			d.height = 20;

			if (GUI.Button(d, textWithTT(SCANcontroller.controller.anomalyMarker, "Toggle Anomalies"), SCANskins.SCAN_buttonBorderless))
			{
				showAnomaly = !showAnomaly;
			}

			fillS(16);

			stopE();
		}

		private void drawMap(int id)
		{
			MapTexture = spotmap.getPartialMap();

			//A blank label used as a template for the actual map texture
			if (IsResizing)
			{
				//Set minimum map size during re-sizing
				dW = resizeW;
				if (dW < WindowSize_Min.x)
					dW = WindowSize_Min.x;
				else if (dW > WindowSize_Max.x)
					dW = WindowSize_Max.x;
				dH = resizeH;
				if (dH < WindowSize_Min.y)
					dH = WindowSize_Min.y;
				else if (dH > WindowSize_Max.y)
					dH = WindowSize_Max.y;

				GUILayout.Label("", GUILayout.Width(dW), GUILayout.Height(dH));
			}
			else
			{
				GUILayout.Label("", GUILayout.Width(MapTexture.width), GUILayout.Height(MapTexture.height));
			}

			TextureRect = GUILayoutUtility.GetLastRect();
			TextureRect.width = spotmap.MapWidth;
			TextureRect.height = spotmap.MapHeight;

			//Stretches the existing map while re-sizing
			if (IsResizing)
			{
				TextureRect.width = dW;
				TextureRect.height = dH;
				GUI.DrawTexture(TextureRect, MapTexture, ScaleMode.StretchToFill);
			}
			else
			{
				GUI.DrawTexture(TextureRect, MapTexture);
			}

		}

		private void mouseOver(int id)
		{
			float mx = Event.current.mousePosition.x - TextureRect.x;
			float my = Event.current.mousePosition.y - TextureRect.y;
			bool in_map = false;
			double mlon = 0, mlat = 0;

			//Draw the re-size label in the corner
			Rect resizer = new Rect(WindowRect.width - 24, WindowRect.height - 26, 24, 24);
			GUI.Label(resizer, SCANskins.SCAN_ResizeIcon);

			//Handles mouse positioning and converting to lat/long coordinates
			if (mx >= 0 && my >= 0 && mx <= TextureRect.width && my <= TextureRect.height  /*mx >= 0 && my >= 0 && mx < MapTexture.width && my < MapTexture.height*/)
			{
				double mlo = spotmap.Lon_Offset + (mx / spotmap.MapScale) - 180;
				double mla = spotmap.Lat_Offset + ((TextureRect.height - my) / spotmap.MapScale) - 90;
				mlon = spotmap.unprojectLongitude(mlo, mla);
				mlat = spotmap.unprojectLatitude(mlo, mla);

				if (mlon >= -180 && mlon <= 180 && mlat >= -90 && mlat <= 90)
				{
					in_map = true;
					if (SCANcontroller.controller.MechJebSelecting)
					{
						SCANcontroller.controller.MechJebSelectingActive = true;
						mjTarget.x = mlon;
						mjTarget.y = mlat;
						SCANcontroller.controller.MechJebTargetCoords = mjTarget;
						Rect r = new Rect(mx + TextureRect.x - 11, my + TextureRect.y - 13, 24, 24);
						SCANuiUtil.drawMapIcon(r, SCANskins.SCAN_MechJebYellowIcon, true);
					}
				}
				else if (SCANcontroller.controller.MechJebSelecting)
					SCANcontroller.controller.MechJebSelectingActive = false;

				if (mlat > 90)
				{
					mlon = (mlon + 360) % 360 - 180;
					mlat = 180 - mlat;
				}
				else if (mlat < -90)
				{
					mlon = (mlon + 360) % 360 - 180;
					mlat = -180 - mlat;
				}
			}
			else if (SCANcontroller.controller.MechJebSelecting)
				SCANcontroller.controller.MechJebSelectingActive = false;

			//Handles mouse click while inside map
			if (Event.current.isMouse)
			{
				if (Event.current.type == EventType.MouseUp)
				{
					//Generate waypoint for MechJeb target
					if (SCANcontroller.controller.MechJebSelecting && SCANcontroller.controller.MechJebSelectingActive && Event.current.button == 0 && in_map)
					{
						SCANwaypoint w = new SCANwaypoint(mlat, mlon, "MechJeb Landing Target");
						SCANcontroller.controller.MechJebTarget = w;
						data.addToWaypoints();
						SCANcontroller.controller.MechJebSelecting = false;
						SCANcontroller.controller.MechJebSelectingActive = false;
					}
					//Middle click re-center
					else if (Event.current.button == 2 || (Event.current.button == 1 && GameSettings.MODIFIER_KEY.GetKey()))
					{
						if (in_map)
						{
							spotmap.centerAround(mlon, mlat);
							resetMap();
						}
					}
					//Right click zoom in
					else if (Event.current.button == 1)
					{
						if (in_map)
						{
							spotmap.MapScale = spotmap.MapScale * 1.25f;
							spotmap.centerAround(mlon, mlat);
							spotmap.resetMap();
						}
					}
					//Left click zoom out
					else if (Event.current.button == 0)
					{
						if (in_map)
						{
							spotmap.MapScale = spotmap.MapScale / 1.25f;
							if (spotmap.MapScale < 2)
								spotmap.MapScale = 2;
							resetMap();
						}
					}
					Event.current.Use();
				}

				//Handle clicking inside the re-size button
				else if (Event.current.isMouse
				&& Event.current.type == EventType.MouseDown
				&& Event.current.button == 0
				&& resizer.Contains(Event.current.mousePosition))
				{
					IsResizing = true;
					dragStart.x = Input.mousePosition.x;
					dragStart.y = Input.mousePosition.y;
					resizeW = TextureRect.width;
					resizeH = TextureRect.height;
					Event.current.Use();
				}
			}

			//Draw the actual mouse over info label below the map
			if (SCANcontroller.controller.MechJebSelecting)
			{
				SCANuiUtil.readableLabel("MechJeb Landing Guidance Targeting...", false);
				fillS(-10);
				SCANuiUtil.mouseOverInfoSimple(mlon, mlat, spotmap, data, spotmap.Body, in_map);
			}
			else if (showInfo)
				SCANuiUtil.mouseOverInfoSimple(mlon, mlat, spotmap, data, spotmap.Body, in_map);
			else
				fillS(10);
		}

		private void mapLabels(int id)
		{
			//Draw the orbit overlays
			if (showOrbit && HighLogic.LoadedSceneIsFlight)
			{
				SCANuiUtil.drawOrbit(TextureRect, spotmap, v, spotmap.Body);
			}

			SCANuiUtil.drawMapLabels(TextureRect, v, spotmap, data, spotmap.Body, showAnomaly, showWaypoints);
		}

	}
}
