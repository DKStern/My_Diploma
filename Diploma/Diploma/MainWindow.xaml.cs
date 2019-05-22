﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.VectorTypes;
using ManagedCuda.CudaFFT;

namespace Diploma
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Drawing.Color StartColor, FinishColor;
        private int fileCount = 0; //Count of files
        private string path; //Path to the directory with data
        private WriteableBitmap bitmap;
        const double maxFreq = 10000.0;
        const int bufSize = 4096;
        private int depth = 1291, frequency = 10000;
        private Logger logger = new Logger();
        private short[] fileBuffer;
        private bool isRequested = true, isReady = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            imageBox.Source = bitmap;
            logger.Add($"Начало работы программы: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Add("");
        }

        private void exitBtn_Click(object sender, RoutedEventArgs e)
        {
            logger.Add($"Завершение работы программы: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Close();
            Close();
        }

        /// <summary>
        /// Создаёт изображение из файл с данными
        /// </summary>
        private void CreateImg()
        {
            logger.Add("Визуализация данных...");
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int width, heigth;
            double max = double.MinValue, min = double.MaxValue;
            System.Drawing.Color color;
            List<string> file = new List<string>();
            string[] str;
            int count = 0;
            int len = 0;
            using (StreamReader sr = new StreamReader("OutPut.txt"))
            {
                logger.Add("Нахождение диапозона значения энергии...");
                var st = new Stopwatch();
                st.Start();
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    count++;
                    if (line[line.Length - 1] == ' ')
                        line = line.Remove(line.Length - 1, 1);
                    str = line.Split(' ');
                    if (len < str.Length)
                        len = str.Length;
                    for (int i = 0; i < str.Length; i++)
                    {
                        var d = Convert.ToDouble(str[i]);
                        if (d > max)
                        {
                            max = d;
                        }
                        if (d < min)
                        {
                            min = d;
                        }
                    }
                }
                st.Stop();
                logger.Add($"Диапазон найден: от {min} до {max}");
                logger.Add($"Нахождение заняло: {st.Elapsed}");
            }
            heigth = count;
            width = len;
            Bitmap bmp = new Bitmap(width, heigth);
            int y = 0;
            using (var sr = new StreamReader("Output.txt"))
            {
                logger.Add("Создание изображения из файла...");
                var st = new Stopwatch();
                st.Start();
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line[line.Length - 1] == ' ')
                        line = line.Remove(line.Length - 1, 1);
                    str = line.Split(' ');
                    for (int x = 0; x < str.Length; x++)
                    {
                        color = GetColors(min, max, Convert.ToDouble(str[x]));
                        bmp.SetPixel(x, y, color);
                    }
                    y++;
                }
                st.Stop();
                logger.Add("Изображение создано");
                logger.Add($"Создание изображения заняло: {st.Elapsed}");
            }

            bmp.Save("Pic.png");
            bmp.Dispose();
            watch.Stop();
            logger.Add("Изображение сохранено: Pic.png");
            ShowImg();
            logger.Add($"Визуализация заняла: {watch.Elapsed}");
            logger.Add("");
            GC.Collect(1, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Приведение размерности входных данных до ближайшей степени двойки
        /// </summary>
        /// <param name="soundLine">Входные данные</param>
        /// <param name="K">Степень</param>
        /// <returns>Новые данные</returns>
        private short[] PowOfTwo(short[] soundLine, ref short K)
        {
            logger.Add("ПОТОК ЧТЕНИЯ ДАННЫХ >> Приводим размерность входных данных к степени двойки...");
            int len = soundLine.Length;
            uint pow = 2;
            short k = 1;
            while (len > pow)
            {
                pow *= 2;
                k++;
            }

            K = k;
            if (len == pow)
                return soundLine;
            else
            {
                short[] newMas = new short[pow];
                for (int i = 0; i < pow; i++)
                {
                    if (i < len)
                        newMas[i] = soundLine[i];
                    else
                        newMas[i] = 0;
                }

                logger.Add("ПОТОК ЧТЕНИЯ ДАННЫХ >> Размерность данных приведена к степени двойки! Показатель степени: " + K.ToString());
                return newMas;
            }
        }

        private void ReaderFile(string[] files)
        {
            int i = 1;
            byte[] byteMas = null;
            short[] soundLine = null;
            byte[] b = new byte[2];
            short K = 0; //Показатель степени
            while (true)
            {
                if (isRequested)
                {
                    if (i <= files.Length)
                    {
                        logger.Add($"ПОТОК ЧТЕНИЯ ДАННЫХ >> Чтение данных из {i} файла...");
                        var sw = new Stopwatch();
                        sw.Start();
                        byteMas = File.ReadAllBytes(files[i - 1]);
                        if (soundLine == null)
                        {
                            soundLine = new short[byteMas.Length / 2];
                        }
                        for (int j = 0; j < byteMas.Length; j += 2)
                        {
                            b[0] = byteMas[j];
                            b[1] = byteMas[j + 1];
                            soundLine[j / 2] = BitConverter.ToInt16(b, 0);
                        }
                        fileBuffer = (short[])PowOfTwo(soundLine, ref K).Clone();
                        sw.Stop();
                        byteMas = null;
                        GC.Collect();
                        logger.Add($"ПОТОК ЧТЕНИЯ ДАННЫХ >> Считываение данных из {i} файла заняло: {sw.Elapsed.ToString()}");
                        //logger.Add("");

                        isRequested = false;
                        isReady = true;

                        if (i == files.Length)
                            return;
                        else
                            i++;
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        private void ReaderFile()
        {
            using (var tdms = new NationalInstruments.Tdms.File(path))
            {
                tdms.Open();

                int fileNumber = 0;
                int count = 0;
                short[] soundLine = null;
                short K = 0; //Показатель степени
                while (true)
                {
                    if (isRequested)
                    {
                        if (fileNumber < tdms.Groups["Measurement"].Channels.Count)
                        {
                            logger.Add($"ПОТОК ЧТЕНИЯ ДАННЫХ >> Чтение данных из {fileNumber} файла...");
                            var sw = new Stopwatch();
                            sw.Start();
                            soundLine = new short[tdms.Groups["Measurement"].Channels[Convert.ToString(fileNumber)].DataCount];
                            count = 0;

                            //var list = tdms.Groups["Measurement"].Channels["0"].GetData<short>().ToArray();

                            foreach (var item in (List<short>)tdms.Groups["Measurement"].Channels[Convert.ToString(fileNumber)].GetData<short>())
                            {
                                soundLine[count] = item;
                                count++;
                                if (count == tdms.Groups["Measurement"].Channels[Convert.ToString(fileNumber)].DataCount)
                                    System.Windows.MessageBox.Show("");
                            }

                            fileBuffer = (short[])PowOfTwo(soundLine, ref K).Clone();
                            sw.Stop();
                            GC.Collect();
                            logger.Add($"ПОТОК ЧТЕНИЯ ДАННЫХ >> Считываение данных из {fileNumber} файла заняло: {sw.Elapsed.ToString()}");

                            isRequested = false;
                            isReady = true;

                            if (fileNumber == tdms.Groups["Measurement"].Channels.Count - 1)
                            {
                                tdms.Dispose();
                                return;
                            }
                            else
                                fileNumber++;
                        }
                        else
                        {
                            tdms.Dispose();
                            return;
                        }
                    }
                }
            }           
        }

        /// <summary>
        /// Get new Index
        /// </summary>
        /// <param name="index">Текущий индекс</param>
        /// <param name="nfft">Коэф. сжатия</param>
        /// <returns>Индекс</returns>
        private int GetIndex(int index, int nfft)
        {
            if (index == 0)
                return -1;
            double idx = Math.Log((double)index * maxFreq / (double)(nfft - 0)) * bufSize / Math.Log(maxFreq);
            if (idx < 0.0)
                return -1;
            return (int)Math.Floor(idx);
        }

        /// <summary>
        /// Get new Index
        /// </summary>
        /// <param name="index">Текущий индекс</param>
        /// <param name="nfft">Коэф. сжатия</param>
        /// <returns>Индекс</returns>
        private int GetIndexLin(int index, int nfft)
        {
            return (int)Math.Floor((double)index * bufSize / nfft);
        }

        /// <summary>
        /// Вычисления
        /// </summary>
        private void Start()
        {
            logger.Add("Получаем список файлов...");
            logger.Add("");
            int buffSize = 0;
            //string[] fileList; //список файлов
            //fileList = Directory.GetFiles(path);
            short counter = 1;
            byte deviceID = 0; //Номер устройства
            cuFloatComplex[] h_data; //Данные в формате CUDA (комплексные) на хосте
            CudaDeviceVariable<cuFloatComplex> d_data; //Входные данные в формате видеокарты

            Stopwatch watch = new Stopwatch();
            Stopwatch stopwatch = new Stopwatch();

            int nfft; //Размерность сжатых данных
            float[] buffer;
            int[] numbers;
            int index;
            string Path = $"Output {DateTime.Now.Day}_{DateTime.Now.Month}_{DateTime.Now.Year} {DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}.txt";
            StreamWriter sw = new StreamWriter(Path, true);

            Task readTask = new Task(() => ReaderFile(/*fileList*/));
            readTask.Start();

            CudaContext ctx = new CudaContext(deviceID, CUCtxFlags.MapHost | CUCtxFlags.BlockingSync); //Создание контекста вычислений на устройстве

            while (true)
            {
                if (isReady)
                {
                    //Копирование
                    stopwatch.Reset();
                    stopwatch.Start();
                    buffSize = fileBuffer.Length;
                    
                    //Вычисления
                    h_data = new cuFloatComplex[buffSize];

                    for (int j = 0; j < buffSize; j++)
                    {
                        h_data[j].real = (float)Convert.ToDouble(fileBuffer[j]);
                        h_data[j].imag = 0;
                    }

                    isRequested = true;
                    isReady = false;

                    d_data = new CudaDeviceVariable<cuFloatComplex>(buffSize); //Создание переменной на видеокарте и выделение памяти под неё
                    d_data.CopyToDevice(h_data); //Копирование входных данных на видеокарту
                    CudaFFTPlan1D plan = new CudaFFTPlan1D(d_data.Size, cufftType.C2C, 1); //Создание плана вычислений !!!Найти и расписать пояснение подробнее

                    watch.Reset();
                    watch.Start();
                    plan.Exec(d_data.DevicePointer, TransformDirection.Forward); //Выполнение вычислений
                    watch.Stop();
                    h_data = d_data;

                    logger.Add($"ПОТОК ОБРАБОТКИ ДАННЫХ >> Вычисление {counter} дорожки на GPU заняло: {watch.Elapsed.ToString()}");

                    //Сжатие данных
                    nfft = h_data.Length / 2 + 1;
                    buffer = new float[bufSize];
                    numbers = new int[bufSize];

                    watch.Reset();
                    watch.Start();
                    int count = 0;
                    for (int j = 0; j < nfft; j++) //до nfft
                    {
                        index = GetIndex(j, nfft); //nfft
                        if (index < 0)
                            count++;
                        if (index >= 0)
                        {
                            buffer[index] += (float)Math.Sqrt(Math.Pow(h_data[j].real / (double)nfft, 2) + Math.Pow(h_data[counter].imag / (double)nfft, 2));
                            numbers[index]++;
                        }
                    }

                    for (int j = 0; j < bufSize; j++)
                    {
                        if (buffer[j] > 0)
                        {
                            buffer[j] /= (float)numbers[j];
                        }
                        else
                            buffer[j] = -1.0f;
                    }
                    watch.Stop();
                    logger.Add($"ПОТОК ОБРАБОТКИ ДАННЫХ >> Сжатие {counter} дорожки заняло: {watch.Elapsed.ToString()}");

                    //Запись готовых данных в файл
                    foreach (var item in buffer)
                    {
                        sw.Write(item + " ");
                    }
                    sw.WriteLine();

                    count++;
                    buffer = null;
                    numbers = null;
                    h_data = null;
                    d_data.Dispose();
                    plan.Dispose();
                    GC.Collect();

                    progressBar.Value++;
                    stopwatch.Stop();
                    logger.Add($"ПОТОК ОБРАБОТКИ ДАННЫХ >> Вычисление {counter++} дорожки заняло: {stopwatch.Elapsed.ToString()}");
                    //logger.Add("");
                }

                if (readTask.Status == TaskStatus.RanToCompletion && !isReady)
                {
                    readTask.Dispose();
                    break;
                }
            }
            CudaContext.ProfilerStop();
            ctx.Dispose();
            sw.Close();
        }

        private void startBtn_Click(object sender, RoutedEventArgs e)
        {
            Stopwatch sw = new Stopwatch();
            logger.Add($"Начало вычислений: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Add("");
            sw.Start();

            //Вычисления на GPU
            Start();

            sw.Stop();
            logger.Add($"Конец вычислений: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Add($"Вычисления заняли: {sw.Elapsed.ToString()}");
            logger.Add("");


            //Создание изображения
            //CreateImg();  
        }

        /// <summary>
        /// Отображение изображения
        /// </summary>
        private void ShowImg()
        {
            BitmapImage bm1 = new BitmapImage();
            bm1.BeginInit();
            bm1.UriSource = new Uri("Pic.png", UriKind.Relative);
            bm1.CacheOption = BitmapCacheOption.OnLoad;
            bm1.EndInit();
            bitmap = new WriteableBitmap(bm1);
            imageBox.Source = bitmap;
        }

        private static System.Drawing.Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return System.Drawing.Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return System.Drawing.Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return System.Drawing.Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return System.Drawing.Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return System.Drawing.Color.FromArgb(255, t, p, v);
            else
                return System.Drawing.Color.FromArgb(255, v, p, q);
        }

        private void startColorPiker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            StartColor = System.Drawing.Color.FromArgb(255, startColorPiker.SelectedColor.Value.R, startColorPiker.SelectedColor.Value.G, startColorPiker.SelectedColor.Value.B);
            logger.Add($"Начальный цвет выбран: {StartColor.ToString()}");
            logger.Add("");
        }

        private void finishColorPiker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            FinishColor = System.Drawing.Color.FromArgb(255, finishColorPiker.SelectedColor.Value.R, finishColorPiker.SelectedColor.Value.G, finishColorPiker.SelectedColor.Value.B);
            logger.Add($"Конечный цвет выбран: {FinishColor.ToString()}");
            logger.Add("");
        }

        private void openTDMS_Click(object sender, RoutedEventArgs e)
        {
            uint startPosition = 0;
            uint length = 0;
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "TDMS file (*.tdms)|*.tdms|All files (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                path = dialog.FileName;
            }

            using (var tdms = new NationalInstruments.Tdms.File(path))
            {
                tdms.Open();

                startPosition = (uint)tdms.Properties["StartPosition[m]"]; //Есть свойство - начальная позиция (начальная глубина в метрах)
                length = (uint)tdms.Properties["MeasureLength[m]"]; //Есть свойство - длина измерения в метрах

                depthStartBox.Text = Convert.ToString(startPosition);
                depthFinishBox.Text = Convert.ToString(startPosition + length);

                tdms.Dispose();
            }

        }

        private void openBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                logger.Add("Ожидается выбор папки...");
                FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    path = folderBrowserDialog.SelectedPath;
                    fileCount = Directory.GetFiles(path).Length;
                    depthFinishBox.Text = Convert.ToString(fileCount);
                    progressBar.Value = 0;
                    progressBar.Maximum = fileCount;
                    //helpMas = null;
                    logger.Add($"Папка выбрана: {path}");
                    logger.Add($"Файлов в папке: {fileCount.ToString()}");
                    logger.Add("");
                }
                else
                {
                    logger.Add("Папка не выбрана!");
                    logger.Add("");
                }
            }
            catch
            {
                logger.Add("Ошибка при открытии папки!");
                logger.Add("");
                System.Windows.MessageBox.Show("Ошибка при открытии папки!");
            }
        }

        private void imageBox_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pointStart = imageBox.TranslatePoint(new System.Windows.Point(0,0),this);
            var pointFinish = new System.Windows.Point(imageBox.ActualWidth, imageBox.ActualHeight);
            var mousePoint = new System.Windows.Point(e.GetPosition(null).X, e.GetPosition(null).Y);
            var xNew = (mousePoint.X - pointStart.X) / imageBox.ActualWidth;
            var yNew = (mousePoint.Y - pointStart.Y) / imageBox.ActualHeight;
            XLabel.Content = $"Частота: {Convert.ToInt32(xNew * frequency)} Гц";
            YLabel.Content = $"Глубина: {Convert.ToInt32(yNew * depth)} метров";
        }

        /// <summary>
        /// Вычисление цвета пикселя
        /// </summary>
        /// <param name="min">Минимальное значение</param>
        /// <param name="max">Максимальное значение</param>
        /// <param name="current">Текущее значение</param>
        /// <returns>Цвет</returns>
        private System.Drawing.Color GetColors(double min, double max, double current)
        {
            System.Drawing.Color color;
            var help1 = max - min;
            var help2 = current - min;
            var help = help2 / help1;

            if (current == -1.0)
                return StartColor;

            //double hStart = StartColor.GetHue();
            //double hFinish = FinishColor.GetHue();
            //var h = hFinish - hStart;
            //h = h * help + min;
            //color = ColorFromHSV(h, 1, 1);

            var dR = Convert.ToInt32(Math.Ceiling(help * (FinishColor.R - StartColor.R)));
            var dG = Convert.ToInt32(Math.Ceiling(help * (FinishColor.G - StartColor.G)));
            var dB = Convert.ToInt32(Math.Ceiling(help * (FinishColor.B - StartColor.B)));
            color = System.Drawing.Color.FromArgb(dR + StartColor.R, dG + StartColor.G, dB + StartColor.B);

            return color;
        }
    }
}
