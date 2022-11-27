﻿using ExifLib;
using Newtonsoft.Json;
using Prism.Navigation;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using SloperMobile.Common.Constants;
using SloperMobile.Common.Interfaces;
using SloperMobile.DataBase;
using SloperMobile.DataBase.DataTables;
using SloperMobile.Model;
using SloperMobile.Model.ResponseModels;
using SloperMobile.Model.SendsModels;
using SloperMobile.Model.TopoModels;
using SloperMobile.ViewModel.ReportedIssueViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using XLabs.Platform.Device;

namespace SloperMobile.UserControls.ReportIssue
{
	public partial class IssueToReportSummary : ContentView
	{
		private readonly IExceptionSynchronizationManager exceptionManager;
		private readonly IRepository<BucketTable> bucketRepository;
		private readonly IRepository<CragImageTable> cragImageRepository;
		private readonly IRepository<TopoTable> topoRepository;
		private readonly IRepository<SectorTable> sectorRepository;
		private readonly IGetImageBytes imageBytesService;
		private IDevice device;
		private TopoImageResponseModel topoImg = null;
		private ReportIssueViewModel reportIssueViewModel;
		private MapListModel currentSector;
		private SendModel send = null;
		private int? routeId;
		private int sectorId;
		private float xRatio;
		private float yRatio;
		private float xOffset;
		private float ratio;
		private float imageWidth;
		private float strokeWidthRatio = 1;	 
		private float initialHeight;
		private double globalWidth;
		private double globalHeight;
		private bool hasBeenRedrawen;
		private bool isFirstTimeRedraw;
		private bool successfullyDrawn;
		private bool isFromGallery;
		private string string64;
		private int hasBeingDrawen;
		private ExifOrientation orientation;

		public IssueToReportSummary(
			IRepository<BucketTable> bucketRepository,
			IRepository<CragImageTable> cragImageRepository,
			IRepository<TopoTable> topoRepository,
			IRepository<SectorTable> sectorRepository,
			IGetImageBytes imageBytesService,
			IExceptionSynchronizationManager exceptionManager)
		{
			InitializeComponent();
			App.IsAscentSummaryPage = true;
			this.bucketRepository = bucketRepository;
			this.cragImageRepository = cragImageRepository;
			this.topoRepository = topoRepository;
			this.sectorRepository = sectorRepository;
			this.imageBytesService = imageBytesService;
			this.exceptionManager = exceptionManager;
			//_Image.IsVisible = false;
		}

		private async void OnPaintSample(object sender, SKPaintSurfaceEventArgs e)
		{
			try
			{
				if (string64 == null && topoImg != null)
				{
					string64 = topoImg?.image.data.Split(',')[1];
				}

				if (reportIssueViewModel.ShareSelected)
				{
					return;
				}

				var success = MainDrawing(e);
				if (!success)
				{
					return;
				}

				LoadCarouselImages();
			}
			catch (Exception exception)
			{
				await exceptionManager.LogException(new ExceptionTable
				{
					Method = nameof(this.OnPaintSample),
					Page = this.GetType().Name,
					StackTrace = exception.StackTrace,
					Exception = exception.Message,
					Data = $" RouteId : {routeId}"
				});
			}
		}

		private void LoadCarouselImages()
		{
			reportIssueViewModel.AscentSummaryImgs = new ObservableCollection<ImageSource>
				{
					ImageSource.FromStream(() => new MemoryStream(reportIssueViewModel.AscentSummaryImagesBytes[0]))
				};

			reportIssueViewModel.AscentSummaryImagesBytes = new List<byte[]>
				{
					reportIssueViewModel.AscentSummaryImagesBytes[0]
				};

			//_Image.IsVisible = false;
			zoomScroll.IsVisible = false;
			string64 = null;
		}

		private bool MainDrawing(SKPaintSurfaceEventArgs e)
		{
			if (!hasBeenRedrawen)
			{
				if (!isFirstTimeRedraw)
				{
					initialHeight = skCanvas.CanvasSize.Width;
				}

				isFirstTimeRedraw = true;
				hasBeenRedrawen = true;
				absoluteLayout.HeightRequest = globalWidth;
				absoluteLayout.WidthRequest = globalWidth;
				skCanvas.Layout((new Rectangle(0, 0, skCanvas.CanvasSize.Width,
					skCanvas.CanvasSize.Width)));
				skCanvas.InvalidateSurface();
				return false;
			}

			if ((skCanvas.CanvasSize.Width < 1000))
			{
				skCanvas.Layout((new Rectangle(0, 0, skCanvas.CanvasSize.Width,
					skCanvas.CanvasSize.Width)));
				skCanvas.InvalidateSurface();
				return false;
			}

			if (topoImg == null)
			{
				return false;
			}


			var canvas = e.Surface.Canvas;
			canvas.Clear();
			if (hasBeingDrawen < 1 && topoImg != null)
			{
				var deviceHeight = scrollViewGrid.Height;
				ratio = (float)deviceHeight / float.Parse(topoImg.image.height);
				globalHeight = deviceHeight;
				globalWidth = double.Parse(topoImg.image.width) * ratio;

				absoluteLayout.HeightRequest = globalHeight;
				absoluteLayout.WidthRequest = globalWidth;
				hasBeingDrawen++;
				skCanvas.InvalidateSurface();
				return false;

			}


			if (string.IsNullOrEmpty(topoImg?.image.data) && reportIssueViewModel?.TakenImageBytes == null)
			{
				return false;
			}

			// decode the bitmap from the stream
			DrawCanvas(e, canvas, 0, 0);
			return true;
		}

		private void DrawCanvas(SKPaintSurfaceEventArgs e, SKCanvas canvas, int minusHeightValue,
			int yOffsetValue)
		{
			var imageBytes = Convert.FromBase64String(string64);
			Draw(e, canvas, yOffsetValue, imageBytes);
		}

		private void Draw(SKPaintSurfaceEventArgs e, SKCanvas canvas, int yOffsetValue, byte[] imageBytes)
		{
			using (var fileStream = new MemoryStream(imageBytes))
			using (var stream = new SKManagedStream(fileStream))
			using (var bitmap = SKBitmap.Decode(stream))
			using (var paint = new SKPaint())
			{
				xRatio = 1;
				xOffset = 0;


				if (bitmap.Info.Width < bitmap.Info.Height)
				{
					DrawPortrait(canvas, yOffsetValue, bitmap, paint);
				}
				else
				{
					DrawLandscape(canvas, bitmap, paint);
				}
																			  
				//code to draw line
				if (reportIssueViewModel.TakenImageBytes == null)
				{
					using (new SKAutoCanvasRestore(canvas, true))
					{
						DrawLine(canvas);
					}
				}

				imageWidth = skCanvas.CanvasSize.Width;
				using (new SKAutoCanvasRestore(canvas, true))
				using (var localPaint = new SKPaint())
				using (var path = new SKPath())
				{
					path.AddRect(SKRect.Create(0, skCanvas.CanvasSize.Width - skCanvas.CanvasSize.Width / 5,
						skCanvas.CanvasSize.Width, skCanvas.CanvasSize.Width / 5));
					localPaint.Shader = SKShader.CreateLinearGradient(
						new SKPoint(0, skCanvas.CanvasSize.Width),
						new SKPoint(0, skCanvas.CanvasSize.Width - skCanvas.CanvasSize.Width / 5),
						new SKColor[] { SKColor.Parse("#000000"), SKColor.Empty },
						null,
						SKShaderTileMode.Clamp);
					canvas.DrawPath(path, localPaint);
				}

				var heightPoint = skCanvas.CanvasSize.Width;
				paint.TextSize = imageWidth * 0.04f;
				paint.IsAntialias = true;
				paint.Color = SKColor.Parse("#FF8E2D");
				paint.TextAlign = SKTextAlign.Left;
                paint.Typeface = SKTypeface.FromFamilyName("Roboto");
                canvas.DrawText($"{reportIssueViewModel.PageHeaderText}", imageWidth * 0.01f, heightPoint - imageWidth * 0.07f,
					paint);
				paint.Color = SKColors.White;
				paint.TextSize = imageWidth * 0.03f;
                canvas.DrawText($"{reportIssueViewModel.PageSubHeaderText}", imageWidth * 0.01f, heightPoint - imageWidth * 0.03f,
					paint);
				DrawLogo(e.Surface.Canvas, imageWidth * 0.01f, imageWidth);

				var imageWithNoStart = e.Surface.Snapshot();
				var dataWithNoStart = imageWithNoStart.Encode(SKEncodedImageFormat.Png, 100);
				var bytesWithNoStart = dataWithNoStart.ToArray();
				reportIssueViewModel.AscentImageWithNoStarts = bytesWithNoStart;
				var string64 = Convert.ToBase64String(bytesWithNoStart);
			}
		}

		private void DrawLandscape(SKCanvas canvas, SKBitmap bitmap, SKPaint paint)
		{
			var imageRatio = skCanvas.CanvasSize.Height / bitmap.Info.Height;

			var diamondPoint = reportIssueViewModel.TakenImageBytes == null
				? FindDiamondPoint(topoImg?.drawing?.FirstOrDefault(model => model.id == routeId))
				: new TopoPointsModel
				{
					x = (bitmap.Info.Width / 2).ToString(),
				};

			var xCoordinate = float.Parse(diamondPoint.x) * imageRatio;
			var halfOfCanvasWidth = skCanvas.CanvasSize.Width / 2;

			//if x coordinate is further than the center of the canvas then calculate x offset,
			//else x offset is 0
			if (xCoordinate > halfOfCanvasWidth)
			{
				//Distance from diamond x coordinate to the end of the image
				var endOffset = bitmap.Info.Width * imageRatio - xCoordinate;

				//If distance from diamond x coordinate to the end of the image is too small then set offset value to width of the canvas
				if (halfOfCanvasWidth > endOffset)
					xOffset = (bitmap.Width * imageRatio - skCanvas.CanvasSize.Width) * -1;
				else
					xOffset = (xCoordinate - halfOfCanvasWidth) * -1;
			}

			yRatio = imageRatio;
			xRatio = imageRatio;

			canvas.DrawBitmap(bitmap, SKRect.Create(xOffset, 0, (float)(bitmap.Info.Width * imageRatio), (float)(bitmap.Info.Height * imageRatio)), paint);
		}

		private void DrawPortrait(SKCanvas canvas, int yOffsetValue, SKBitmap bitmap, SKPaint paint)
		{
			using (var localPaint = new SKPaint())
			using (var skImage = SKImage.FromBitmap(bitmap))
			{
				localPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Solid, 90f);
				localPaint.ImageFilter = SKImageFilter.CreateBlur(90f, 90f);
				canvas.DrawImage(skImage,
					SKRect.Create((float)xOffset, yOffsetValue, (float)(skCanvas.CanvasSize.Width),
						(float)(skCanvas.CanvasSize.Width)), localPaint);
			}

			yRatio = skCanvas.CanvasSize.Width / bitmap.Info.Height;
			xRatio = skCanvas.CanvasSize.Width / bitmap.Info.Height;
			xOffset = (skCanvas.CanvasSize.Width - bitmap.Info.Width * xRatio) / 2;
			canvas.DrawBitmap(bitmap,
				SKRect.Create(xOffset, 0,
					bitmap.Info.Width * xRatio,
					skCanvas.CanvasSize.Width), paint);
		}

		private SKBitmap Rotate(SKBitmap bitmap, int deegre)
		{
			var rotated = new SKBitmap(bitmap.Height, bitmap.Width);
			using (var surface = new SKCanvas(rotated))
			{
				surface.Translate(rotated.Width, 0);
				surface.RotateDegrees(deegre);
				surface.DrawBitmap(bitmap, 0, 0);
			}

			return rotated;
		}

		private TopoPointsModel FindDiamondPoint(TopoDrawingModel topoDrawingModel)
		{
			try
			{
				if (topoDrawingModel == null)
				{
					return new TopoPointsModel
					{
						x = (Convert.ToSingle(topoImg.image.width) / 2).ToString(),
					};
				}

				//The diamond point is always the lowest point
				var linePoints = topoDrawingModel?.line?.points;
				var lowestPoint = linePoints[0];
				foreach (var point in linePoints)
				{
					if (double.Parse(point.y) > double.Parse(lowestPoint.y))
						lowestPoint = point;
				}

				return lowestPoint;
			}
			catch (Exception exception)
			{
				return new TopoPointsModel
				{
					x = (Convert.ToSingle(topoImg.image.width) / 2).ToString(),
				};
			}
		}

		private void SetupStars(SKCanvas skCanvas, float heightPoint, float width)
		{
			var fullStarBytes = imageBytesService.GetImageBytes("icon_star_full_100w.png");
			var fullStarIcon = SKData.CreateCopy(fullStarBytes);
			var emptyStarBytes = imageBytesService.GetImageBytes("icon_star_empty_100w.png");
			var emptyStarIcon = SKData.CreateCopy(emptyStarBytes);
			var viewModel = reportIssueViewModel;

			skCanvas.DrawImage(SKImage.FromEncodedData(viewModel.SendRating > 0 ? fullStarIcon : emptyStarIcon),
				SKRect.Create(new SKPoint(width - imageWidth * 0.12f, heightPoint + 1 * imageWidth * 0.08f + imageWidth * 0.04f), new SKSize(imageWidth * 0.08f, imageWidth * 0.08f)));
			skCanvas.DrawImage(SKImage.FromEncodedData(viewModel.SendRating > 1 ? fullStarIcon : emptyStarIcon),
				SKRect.Create(new SKPoint(width - imageWidth * 0.12f, heightPoint + 2 * imageWidth * 0.08f + imageWidth * 0.04f), new SKSize(imageWidth * 0.08f, imageWidth * 0.08f)));
			skCanvas.DrawImage(SKImage.FromEncodedData(viewModel.SendRating > 2 ? fullStarIcon : emptyStarIcon),
				SKRect.Create(new SKPoint(width - imageWidth * 0.12f, heightPoint + 3 * imageWidth * 0.08f + imageWidth * 0.04f), new SKSize(imageWidth * 0.08f, imageWidth * 0.08f)));
			skCanvas.DrawImage(SKImage.FromEncodedData(viewModel.SendRating > 3 ? fullStarIcon : emptyStarIcon),
				SKRect.Create(new SKPoint(width - imageWidth * 0.12f, heightPoint + 4 * imageWidth * 0.08f + imageWidth * 0.04f), new SKSize(imageWidth * 0.08f, imageWidth * 0.08f)));
			skCanvas.DrawImage(SKImage.FromEncodedData(viewModel.SendRating == 5 ? fullStarIcon : emptyStarIcon),
				SKRect.Create(new SKPoint(width - imageWidth * 0.12f, heightPoint + 5 * imageWidth * 0.08f + imageWidth * 0.04f), new SKSize(imageWidth * 0.08f, imageWidth * 0.08f)));
		}

		private void DrawLogo(SKCanvas skCanvas, float heightPoint, float width)
		{
			var logoBytes = imageBytesService.GetImageBytes("logo_sloper_215w.png");
			var logoIcon = SKData.CreateCopy(logoBytes);
			skCanvas.DrawImage(SKImage.FromEncodedData(logoIcon),
				SKRect.Create(new SKPoint(width - imageWidth * 0.14f, heightPoint + imageWidth * 0.01f), new SKSize(imageWidth * 0.12f, imageWidth * 0.08f)));
		}

		private void SetupRouteIcons(SKCanvas skCanvas, float heightPoint, float width)
		{
			var iconPositionIterator = 1;
			var viewModel = reportIssueViewModel;
			var sarr1 = new string[] { };
			var sarr2 = new string[] { };
			var sarr3 = new string[] { };

			if (viewModel.SendClimbingStyle != null && viewModel.SendClimbingStyle.Contains(","))
			{
				sarr1 = viewModel.SendClimbingStyle.Split(',');
			}
			else
			{
				sarr1 = new[] { viewModel.SendClimbingStyle };
			}

			if (viewModel.SendHoldType != null && viewModel.SendHoldType.Contains(","))
			{
				sarr2 = viewModel.SendHoldType.Split(',');
			}
			else
			{
				sarr2 = new[] { viewModel.SendHoldType };
			}

			if (viewModel.SendRouteCharacteristics != null && viewModel.SendRouteCharacteristics.Contains(","))
			{
				sarr3 = viewModel.SendRouteCharacteristics.Split(',');
			}
			else
			{
				sarr3 = new[] { viewModel.SendRouteCharacteristics };
			}

			for (var i = 0; i < sarr1.Length; i++)
			{
				if (sarr1.Length == 0 || (sarr1.Length == 1 && sarr1[0] == null) || sarr1[0] == "0")
				{
					continue;
				}

				var bytes = new byte[] { };
				switch (sarr1[i])
				{
					case "1":
						bytes = imageBytesService.GetImageBytes("icon_steepness_1_slab_border_80x80.png");
						break;
					case "2":
						bytes = imageBytesService.GetImageBytes("icon_steepness_2_vertical_border_80x80.png");
						break;
					case "4":
						bytes = imageBytesService.GetImageBytes("icon_steepness_4_overhanging_border_80x80.png");
						break;
					case "8":
						bytes = imageBytesService.GetImageBytes("icon_steepness_8_roof_border_80x80.png");
						break;
					default:
						bytes = imageBytesService.GetImageBytes("icon_steepness_1_slab_border_80x80.png");
						break;
				}

				var data = SKData.CreateCopy(bytes);
				skCanvas.DrawImage(SKImage.FromEncodedData(data),
					SKRect.Create(
						new SKPoint(width - iconPositionIterator * imageWidth * 0.1f,
							heightPoint - imageWidth * 0.1f),
						new SKSize(imageWidth * 0.08f, imageWidth * 0.08f)));
				i = sarr1.Length;
				iconPositionIterator++;
			}

			for (int i = 0; i < sarr2.Length; i++)
			{
				if (sarr2.Length == 0 || (sarr2.Length == 1 && sarr2[0] == null) || sarr2[0] == "0")
				{
					continue;
				}

				var bytes = new byte[] { };
				switch (sarr2[i])
				{
					case "1":
						bytes = imageBytesService.GetImageBytes("icon_hold_type_1_slopers_border_80x80.png");
						break;
					case "2":
						bytes = imageBytesService.GetImageBytes("icon_hold_type_2_crimps_border_80x80.png");
						break;
					case "4":
						bytes = imageBytesService.GetImageBytes("icon_hold_type_4_jugs_border_80x80.png");
						break;
					case "8":
						bytes = imageBytesService.GetImageBytes("icon_hold_type_8_pockets_border_80x80.png");
						break;
					case "16":
						bytes = imageBytesService.GetImageBytes("icon_hold_type_16_pinches_border_80x80.png");
						break;
					case "32":
						bytes = imageBytesService.GetImageBytes("icon_hold_type_32_jams_border_80x80.png");
						break;
					default:
						bytes = imageBytesService.GetImageBytes("icon_hold_type_1_slopers_border_80x80.png");
						break;
				}

				var data = SKData.CreateCopy(bytes);
				skCanvas.DrawImage(SKImage.FromEncodedData(data),
					SKRect.Create(
						new SKPoint(width - iconPositionIterator * imageWidth * 0.1f - (iconPositionIterator - 2) * imageWidth * 0.01f,
							heightPoint - imageWidth * 0.1f),
						new SKSize(imageWidth * 0.08f, imageWidth * 0.08f)));
				i = sarr2.Length;
				iconPositionIterator++;
			}

			for (int i = 0; i < sarr3.Length; i++)
			{
				if (sarr3.Length == 0 || (sarr3.Length == 1 && sarr3[0] == null) || sarr3[0] == "0")
				{
					continue;
				}

				var bytes = new byte[] { };
				switch (sarr3[i])
				{
					case "1":
						bytes = imageBytesService.GetImageBytes("icon_route_style_1_technical_border_80x80.png");
						break;
					case "2":
						bytes = imageBytesService.GetImageBytes("icon_route_style_2_sequential_border_80x80.png");
						break;
					case "4":
						bytes = imageBytesService.GetImageBytes("icon_route_style_4_powerful_border_80x80.png");
						break;
					case "8":
						bytes = imageBytesService.GetImageBytes("icon_route_style_8_sustained_border_80x80.png");
						break;
					case "16":
						bytes = imageBytesService.GetImageBytes("icon_route_style_16_one_move_border_80x80.png");
						break;
					case "32":
						bytes = imageBytesService.GetImageBytes("icon_route_style_32_exposed_border_80x80.png");
						break;
					default:
						bytes = imageBytesService.GetImageBytes("icon_route_style_1_technical_border_80x80.png");
						break;
				}

				var data = SKData.CreateCopy(bytes);
				skCanvas.DrawImage(SKImage.FromEncodedData(data),
					SKRect.Create(
						new SKPoint(width - iconPositionIterator * (imageWidth * 0.1f) - (iconPositionIterator - 3) * imageWidth * 0.01f,
							heightPoint - imageWidth * 0.1f),
						new SKSize(imageWidth * 0.08f, imageWidth * 0.08f)));
				i = sarr3.Length;
				iconPositionIterator++;
			}
		}

		public void DrawLine(SKCanvas _skCanvas)
		{
			if (topoImg?.drawing == null)
			{
				return;
			}

			using (var path = new SKPath())
			{
				for (int j = 0; j < topoImg.drawing.Count; j++)
				{
					if (routeId == 0)
					{
						DrawPathAndAnnotation(
							_skCanvas,
							j,
							HexToColor(topoImg.drawing[j].line.style.color),
							Device.RuntimePlatform == Device.Android ? SKPaintStyle.Fill : SKPaintStyle.Stroke);
					}
					else if (routeId == topoImg.drawing[j].id)
					{
						DrawPathAndAnnotation(
							_skCanvas,
							j,
							HexToColor(topoImg.drawing[j].line.style.color),
							Device.RuntimePlatform == Device.Android ? SKPaintStyle.Fill : SKPaintStyle.Stroke);
					}
				}

				path.Close();
			}
		}

		private void DrawPathAndAnnotation(
			SKCanvas _skCanvas,
			int j,
			SKColor color,
			SKPaintStyle paintStyle)
		{
			float ptx1, ptx2 = 0, pty1, pty2 = 0;
			var drawingLine = topoImg.drawing[j].line;
			var strokeWidth = Convert.ToSingle(drawingLine.style.width) *
							  (Device.RuntimePlatform == Device.Android ? 1.5f * Convert.ToSingle(drawingLine.style.width) * Convert.ToSingle(drawingLine.style.width) :
							  (float)device.Display.Scale > 2 ? Convert.ToSingle(drawingLine.style.width) * Convert.ToSingle(drawingLine.style.width) : Convert.ToSingle(drawingLine.style.width));

			var pathEffect = SKPathEffect.CreateDash(drawingLine.style.dashPattern
				.Select(item => item *
				(Device.RuntimePlatform == Device.Android ? 1.5f * Convert.ToSingle(drawingLine.style.width) * Convert.ToSingle(drawingLine.style.width) :
							  (float)device.Display.Scale > 2 ? (float)device.Display.Scale * Convert.ToSingle(drawingLine.style.width) * Convert.ToSingle(drawingLine.style.width) : (float)device.Display.Scale * Convert.ToSingle(drawingLine.style.width)))
				.ToArray(), 0);
			
			for (int k = 0; k < drawingLine.points.Count; k++)
			{
				ptx1 = float.Parse(drawingLine.points[k].x) * xRatio + (float)xOffset;
				pty1 = float.Parse(drawingLine.points[k].y) * yRatio;
				if (k != (drawingLine.points.Count - 1))
				{
					ptx2 = float.Parse(drawingLine.points[k + 1].x) * xRatio + (float)xOffset;
					pty2 = float.Parse(drawingLine.points[k + 1].y) * yRatio;
				}

				var rethinLinePaint = new SKPaint
				{
					Style = paintStyle,
					Color = color,
					StrokeWidth = strokeWidth,
					PathEffect = pathEffect
				};

				//draw line
				_skCanvas.DrawLine(ptx1, pty1, ptx2, pty2, rethinLinePaint);
			}

			//draw annotation
			DrawAnnotation(drawingLine, _skCanvas, ratio, topoImg.drawing[j].gradeBucket, (j + 1), topoImg.drawing[j].id, routeId);
		}

		public async void DrawAnnotation(TopoLineModel topoimgTop, SKCanvas _skCanvas, float ratio, string gradeBucket, int _routecnt, int id, int? _routeId)
		{
			for (int i = 0; i < topoimgTop.points.Count; i++)
			{
				string strimg64 = string.Empty;

				if (topoimgTop.points[i].type == "1")
				{
					SetupRectangular(topoimgTop, _skCanvas, i);
				}
				else if (topoimgTop.points[i].type == "3")
				{
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type3IsDarkUnCheckedAndroid
							: ImageByte64Strings.Type3IsDarkUnCheckediOS;
						int minusX = Device.RuntimePlatform == Device.Android ? (float)device.Display.Scale > 3 ? 65 : 32 : (float)device.Display.Scale > 2 ? 28 : 30;
						SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, minusX, 10);
				}
				else if (topoimgTop.points[i].type == "22")
				{
					strimg64 = Device.RuntimePlatform == Device.Android
							  ? ImageByte64Strings.Type3IsDarkCheckedAndroid
							  : ImageByte64Strings.Type3IsDarkCheckediOS;
					//SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, 22, 15);
					int minusX = Device.RuntimePlatform == Device.Android ? (float)device.Display.Scale > 3 ? 65 : 32 : (float)device.Display.Scale > 2 ? 28 : 30;
					SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, minusX, 10);
				}
				else if (topoimgTop.points[i].type == "2")
				{
					strimg64 = Device.RuntimePlatform == Device.Android
						? ImageByte64Strings.Type2IsDarkCheckedAndroid
						: ImageByte64Strings.Type2IsDarkCheckediOS;
					SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64,
						Device.RuntimePlatform == Device.Android ? (float)device.Display.Scale > 3 ? 42 : 22 : (float)device.Display.Scale > 2 ? 18 : 22, 18);
				}
				else if (topoimgTop.points[i].type == "17")
				{
					if (topoimgTop.style.is_dark_checked)
					{
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type17IsDarkCheckedAndroid
							: ImageByte64Strings.Type17IsDarkCheckediOS;
						SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, 22, 15);
					}
					else
					{
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type17IsDarUnkCheckedAndroid
							: ImageByte64Strings.Type17IsDarUnkCheckediOS;
						int minusX = Device.RuntimePlatform == Device.Android ? 40 : 22;
						SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, minusX, 15);
					}
				}
				else if (topoimgTop.points[i].type == "4")
				{
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type4IsDarkUnCheckedAndroid
							: ImageByte64Strings.Type4IsDarkUnCheckediOS;
						SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, (float)device.Display.Scale > 3 ? 22 : 10, 14);
				}
				else if (topoimgTop.points[i].type == "23")
				{
					strimg64 = Device.RuntimePlatform == Device.Android
							 ? ImageByte64Strings.Type4IsDarkCheckedAndroid
							 : ImageByte64Strings.Type4IsDarkCheckediOS;
					//SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, 8, 14);
					SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, (float)device.Display.Scale > 3 ? 22 : 10, 14);
				}
				else if (topoimgTop.points[i].type == "18")
				{
					if (topoimgTop.style.is_dark_checked)
					{
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type18IsDarkCheckedAndroid
							: ImageByte64Strings.Type18IsDarkCheckediOS;
						SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, 8, 14);
					}
					else
					{
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type18IsDarUnkCheckedAndroid
							: ImageByte64Strings.Type18IsDarUnkCheckediOS;
						SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, 8, 14);
					}
				}
				else if (topoimgTop.points[i].type == "8")
				{
					if (topoimgTop.style.is_dark_checked)
					{
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type8IsDarkCheckedAndroid
							: ImageByte64Strings.Type8IsDarkCheckediOS;
						SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, 14, 14);
					}
					else
					{
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type8IsDarkUnCheckedAndroid
							: ImageByte64Strings.Type8IsDarkUnCheckediOS;
						if (Device.RuntimePlatform == Device.Android)
						{
							SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, (float)device.Display.Scale > 3 ? 45 : 22, 26);
						}
						else
						{
							SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, (float)device.Display.Scale > 2 ? 18 : 20, 14);
						}
					}
				}
				else if (topoimgTop.points[i].type == "16")
				{
					if (topoimgTop.style.is_dark_checked)
					{
						//16
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type16IsDarkCheckedAndroid
							: ImageByte64Strings.Type16IsDarkCheckediOS;
						SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, 15, 15);
					}
					else
					{
						strimg64 = Device.RuntimePlatform == Device.Android
							? ImageByte64Strings.Type16IsDarkUnckeckedAndroid
							: ImageByte64Strings.Type16IsDarkUncheckediOS;
						if (Device.RuntimePlatform == Device.Android)
						{
							SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, 28, 28);
						}
						else
						{
							SetupAnnotationIcon(topoimgTop, _skCanvas, ratio, i, strimg64, 15, 15);
						}
					}
				}
				//showing arrow logic   
				var arrowlist = topoimgTop.pointsText.Where(x => x.isdirection.ToLower() == "true").ToList();
				foreach (var item in arrowlist)
				{
					if (item.point_id.Split('p')[1] == i.ToString())
					{
						SetupArrow(topoimgTop, _skCanvas, i);
					}
				}
			}
		}

		private void SetupArrow(TopoLineModel topoimgTop, SKCanvas skCanvas, int i)
		{
			var path = new SKPath();
			var fillPaint = new SKPaint
			{
				Style = SKPaintStyle.Fill,
				Color = SKColor.Parse(topoimgTop.marker.fillColor)
			};

			var leftRightdist = Device.RuntimePlatform == Device.Android ? ((float)device.Display.Scale > 3) ? 120 : 70 : ((float)device.Display.Scale > 2) ? 65 : 30;
			path = new SKPath();
			path.MoveTo(float.Parse(topoimgTop.points[i].x) * xRatio + xOffset, (float.Parse(topoimgTop.points[i].y) * yRatio));
			path.LineTo(float.Parse(topoimgTop.points[i].x) * xRatio + xOffset - leftRightdist, float.Parse(topoimgTop.points[i].y) * yRatio + leftRightdist);
			path.LineTo(float.Parse(topoimgTop.points[i].x) * xRatio + xOffset + leftRightdist, float.Parse(topoimgTop.points[i].y) * yRatio + leftRightdist);
			path.LineTo(float.Parse(topoimgTop.points[i].x) * xRatio + xOffset, float.Parse(topoimgTop.points[i].y) * yRatio);
			skCanvas.DrawPath(path, fillPaint);
		}

		private void SetupRectangular(TopoLineModel topoimgTop, SKCanvas skCanvas, int i)
		{
			var path = new SKPath();
			var fillPaint = new SKPaint
			{
				Style = SKPaintStyle.Fill,
				Color = SKColors.White
			};

			var incrementFirstValue = Device.RuntimePlatform == Device.Android ? (float)device.Display.Scale > 3 ? 55f : 45f : ((float)device.Display.Scale > 2) ? 50f : 26f;
			
			path.MoveTo(float.Parse(topoimgTop.points[i].x) * xRatio - incrementFirstValue + xOffset, (float.Parse(topoimgTop.points[i].y) * yRatio));
			path.LineTo(
				float.Parse(topoimgTop.points[i].x) * xRatio + xOffset,
				float.Parse(topoimgTop.points[i].y) * yRatio - incrementFirstValue);
			path.LineTo(
				float.Parse(topoimgTop.points[i].x) * xRatio + xOffset,
				float.Parse(topoimgTop.points[i].y) * yRatio + incrementFirstValue);

			skCanvas.DrawPath(path, fillPaint);

			path = new SKPath();
			path.MoveTo(float.Parse(topoimgTop.points[i].x) * xRatio + incrementFirstValue + xOffset, (float.Parse(topoimgTop.points[i].y) * yRatio));
			path.LineTo(
				float.Parse(topoimgTop.points[i].x) * xRatio + xOffset,
				float.Parse(topoimgTop.points[i].y) * yRatio + incrementFirstValue);
			path.LineTo(
				float.Parse(topoimgTop.points[i].x) * xRatio + xOffset,
				float.Parse(topoimgTop.points[i].y) * yRatio - incrementFirstValue);
			skCanvas.DrawPath(path, fillPaint);

			fillPaint = new SKPaint
			{
				Style = SKPaintStyle.Fill,
				Color = SKColor.Parse(topoimgTop.marker.fillColor)
			};

			path = new SKPath();
			var incrementSecondCubeRatio = Device.RuntimePlatform == Device.Android ? (float)device.Display.Scale > 3 ? 50f : 40f : ((float)device.Display.Scale > 2) ? 45f : 24f;

			path.MoveTo(float.Parse(topoimgTop.points[i].x) * xRatio - incrementSecondCubeRatio + xOffset, (float.Parse(topoimgTop.points[i].y) * yRatio));
			path.LineTo(
				float.Parse(topoimgTop.points[i].x) * xRatio + xOffset,
				float.Parse(topoimgTop.points[i].y) * yRatio - incrementSecondCubeRatio);
			path.LineTo(
				float.Parse(topoimgTop.points[i].x) * xRatio + xOffset,
				float.Parse(topoimgTop.points[i].y) * yRatio + incrementSecondCubeRatio);

			skCanvas.DrawPath(path, fillPaint);

			path = new SKPath();
			path.MoveTo(float.Parse(topoimgTop.points[i].x) * xRatio + incrementSecondCubeRatio + xOffset, (float.Parse(topoimgTop.points[i].y) * yRatio));
			path.LineTo(
				float.Parse(topoimgTop.points[i].x) * xRatio + xOffset,
				float.Parse(topoimgTop.points[i].y) * yRatio + incrementSecondCubeRatio);
			path.LineTo(
				float.Parse(topoimgTop.points[i].x) * xRatio + xOffset,
				float.Parse(topoimgTop.points[i].y) * yRatio - incrementSecondCubeRatio);
			skCanvas.DrawPath(path, fillPaint);
		}

		private void SetupAnnotationIcon(TopoLineModel topoimgTop, SKCanvas _skCanvas, float ratio, int i, string strimg64, int minusX, int minusY)
		{
			var imageBytes = Convert.FromBase64String(strimg64);
			Stream fileStream = new MemoryStream(imageBytes);

			var sizeOfAnnotation = Device.RuntimePlatform == Device.Android ? 55f : ((float)device.Display.Scale > 2) ? 25f : 22f;
			//if (ascentSummaryViewModel.ShareSelected)
			//{
			sizeOfAnnotation = sizeOfAnnotation * (Device.RuntimePlatform == Device.Android ? (float)device.Display.Scale > 3 ? 3 : 2 : ((float)device.Display.Scale > 2) ? 5 : 2);
			//}

			// decode the bitmap from the stream            
			using (var stream = new SKManagedStream(fileStream))
			using (var bitmap = SKBitmap.Decode(stream))
			using (var paint = new SKPaint())
			{
				_skCanvas.DrawBitmap(
					bitmap,
					SKRect.Create(
					(float.Parse(topoimgTop.points[i].x) * xRatio) - minusX * (Device.RuntimePlatform == Device.Android ? 2 : ((float)device.Display.Scale > 2) ? 3 : 2) + xOffset,
					(float.Parse(topoimgTop.points[i].y) * yRatio) - minusY * (Device.RuntimePlatform == Device.Android ? 2 : ((float)device.Display.Scale > 2) ? 3 : 2),
					sizeOfAnnotation,
					sizeOfAnnotation),
					paint);
			}
		}

		public static SKColor HexToColor(string color)
		{
			if (color.StartsWith("#"))
				color = color.Remove(0, 1);

			byte r, g, b;
			if (color.Length == 3)
			{
				r = Convert.ToByte(color[0] + "" + color[0], 16);
				g = Convert.ToByte(color[1] + "" + color[1], 16);
				b = Convert.ToByte(color[2] + "" + color[2], 16);
			}
			else if (color.Length == 6)
			{
				r = Convert.ToByte(color[0] + "" + color[1], 16);
				g = Convert.ToByte(color[2] + "" + color[3], 16);
				b = Convert.ToByte(color[4] + "" + color[5], 16);
			}
			else
			{
				throw new ArgumentException("Hex color " + color + " is invalid.");
			}
			return new SKColor(r, g, b);
		}


		public void OnNavigatedTo(NavigationParameters parameters)
		{
			if (!successfullyDrawn)
			{
				skCanvas.InvalidateSurface();
			}
		}

		public async void OnNavigatingTo(NavigationParameters parameters)
		{
			try
			{
				if (parameters.Count == 0)
				{
					return;
				}

				reportIssueViewModel = (BindingContext as ReportIssueViewModel);
				this.routeId = (int)parameters[NavigationParametersConstants.CurrentRouteIdParameter];
				this.currentSector = (MapListModel)parameters[NavigationParametersConstants.CurrentSectorParameter];
				reportIssueViewModel.TakenImageBytes = null;
				device = XLabs.Ioc.Resolver.Resolve<IDevice>();
				var topoListData = await topoRepository.FindAsync(sec => sec.sector_id == currentSector.SectorId);
				var topoImages = JsonConvert.DeserializeObject<List<TopoImageResponseModel>>(topoListData?.topo_json);
				if (topoImages == null || topoImages.Count == 0)
				{
					topoImg = await MapModelHelper.GetDefaultTopo(cragImageRepository);
				}
				else
				{
					topoImg = topoImages.FirstOrDefault(item => item.drawing.Any(routes => routes.id == routeId)) == null ?
						new TopoImageResponseModel
						{
							drawing = null,
							image = topoImages.FirstOrDefault().image
						}
						: topoImages.FirstOrDefault(item => item.drawing.Any(routes => routes.id == routeId));
				}

				string64 = topoImg?.image.data.Split(',')[1];

				if (reportIssueViewModel.TakenImageBytes != null)
				{
					reportIssueViewModel.AscentSummaryImagesToShareBytes = new List<byte[]>
					{
						reportIssueViewModel.TakenImageBytes
					};

					//var topoListData = await topoRepository.FindAsync(sec => sec.sector_id == currentSector.SectorId);
					//var topoImages = JsonConvert.DeserializeObject<List<TopoImageResponseModel>>(topoListData.topo_json);
					//topoImg = topoImages.FirstOrDefault(item => item.drawing.Any(routes => routes.id == routeId)) == null ?
					//		new TopoImageResponseModel
					//		{
					//			drawing = null,
					//			image = topoImages.FirstOrDefault().image
					//		}
					//		: topoImages.FirstOrDefault(item => item.drawing.Any(routes => routes.id == routeId));
					//string64 = topoImg?.image.data.Split(',')[1];
				}
				else
				{
					reportIssueViewModel.AscentSummaryImagesToShareBytes = new List<byte[]>
					{
						Convert.FromBase64String(string64)
					};
				}

				reportIssueViewModel.AscentSummaryImagesBytes = reportIssueViewModel.AscentSummaryImagesToShareBytes;

				if (Device.RuntimePlatform == Device.iOS)
				{
					skCanvas.InvalidateSurface();
				}
			}
			catch (Exception exception)
			{
				if (parameters.TryGetValue<MapListModel>(NavigationParametersConstants.SelectedSectorObjectParameter, out var sector))
				{
					sector.SectorImage = null;
					sector.Steepness1 = null;
					sector.Steepness2 = null;
				}


				await exceptionManager.LogException(new ExceptionTable
				{
					Method = nameof(this.OnNavigatingTo),
					Page = this.GetType().Name,
					StackTrace = exception.StackTrace,
					Exception = exception.Message,
					Data = $"currentSector = {JsonConvert.SerializeObject(sector)}, routeId = {JsonConvert.SerializeObject(parameters[NavigationParametersConstants.RouteIdParameter])}, SendModel  = {JsonConvert.SerializeObject(parameters[NavigationParametersConstants.SendModelParameter])}"
				});
			}
		}
	}
}
