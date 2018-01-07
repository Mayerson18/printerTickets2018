using System;
using System.Net;
using System.IO;
using System.Text;
using System.Web.Http.Cors;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Printing;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Threading;
using System.Diagnostics;
using BarcodeLib;
using Gma.QrCodeNet.Encoding;
using Gma.QrCodeNet.Encoding.Windows.Render;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace WindowsFormsApp1
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());
            CreateLListener();
        }

        static public void CreateLListener()
        {

            HttpListener server = new HttpListener();
            server.Prefixes.Add("http://localhost:8888/");//Abre el puerto
            server.Start();
            while (true)
            {
                ThreadPool.QueueUserWorkItem(servidor, server.GetContext());
            }
        }

        public static void servidor(object o)
        {

            var context = o as HttpListenerContext;
            //HttpListenerContext context = server.GetContext();
            HttpListenerResponse response = context.Response;
            response.AppendHeader("Access-Control-Allow-Origin", "*");
            response.AppendHeader("Access-Control-Allow-Methods", "POST, GET");
            response.AppendHeader("Access-Control-Allow-Headers", "Content-Type, Access-Control-Allow-Headers, Authorization, X-Requested-With");
            HttpListenerRequest request = context.Request;
            JArray c = new JArray();
            string msg;
            if (request.HttpMethod == "POST")//Valido que sea un post
            {
                string line;//string donde guardo la linea
                StreamReader sr = new StreamReader(request.InputStream);//Leo un stream
                line = sr.ReadToEnd();//Hasta que este al final de la linea (\0)
                PrintReceiptForTransaction(line);//Funcion que imprime
            }
            else if (request.HttpMethod == "GET")
            {
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    c.Add(printer);
                }
            }
            msg = c.ToString();//lo guardo en un string
            byte[] buffer = Encoding.UTF8.GetBytes(msg);

            response.ContentLength64 = buffer.Length;
            Stream st = response.OutputStream;
            st.Write(buffer, 0, buffer.Length);

            context.Response.Close();

        }
        public static void PrintReceiptForTransaction(string line)
        {
            Console.WriteLine(JArray.Parse(line));
            JArray a = (JArray.Parse(line));
            //Console.WriteLine(a);
            var nombre = a.Children<JObject>().Properties().FirstOrDefault(z => z.Name == "printer");
            JObject obj2 = JObject.Parse(a.First.ToString());
            string type = (string)obj2["type"];
            //Console.WriteLine(a);El array
            //Console.WriteLine(obj2);El objeto dentro del array
            if (type == "ticket")
            {
                string imagen = a.Children<JObject>().Properties().FirstOrDefault(z => z.Name == "zone").Value.ToString();
                Font prueba = new Font("Arial", 20, FontStyle.Regular);
                System.Drawing.Image img;
                JArray b = JArray.Parse(a.Children<JObject>().Properties().FirstOrDefault(z => z.Name == "ticket").Value.ToString());
                if (!String.IsNullOrEmpty(imagen))
                    img = DrawText(imagen, prueba, Color.White, Color.Black);
                else
                    img = null;
                foreach (JObject o2 in b.Children<JObject>())
                {
                    PrintDocument recordDoc = new PrintDocument();
                    recordDoc.DocumentName = "Customer Receipt";
                    recordDoc.PrintController = new StandardPrintController(); // hides status dialog popup
                    PrinterSettings ps = new PrinterSettings();
                    ps.PrinterName = nombre.Value.ToString();
                    recordDoc.PrinterSettings = ps;
                    recordDoc.PrintPage += (sender, args) => PrintReceiptPage(sender, args, o2, img, a);
                    recordDoc.Print();
                    recordDoc.Dispose();

                    bool coleta = (bool)(obj2["coleta"]);
                    if (coleta)
                    {
                        PrintDocument recordDoc2 = new PrintDocument();
                        recordDoc2.DocumentName = "Customer Receipt";
                        recordDoc2.PrintController = new StandardPrintController(); // hides status dialog popup
                        PrinterSettings ps2 = new PrinterSettings();
                        ps2.PrinterName = nombre.Value.ToString();
                        recordDoc2.PrinterSettings = ps2;
                        string serie = (string)(o2["serie"]);
                        string station = (string)(obj2["station"]);
                        string price = (string)(obj2["price"]);
                        string iva = (string)(obj2["iva"]);
                        string total = (string)(obj2["total"]);
                        float x = 10, y = 0, w = 255.59F, h = 0F;
                        recordDoc2.PrintPage += (sender, args) => Coleta(ref args, x, ref y, w, h, serie, station, price, iva, total);
                        recordDoc2.Print();
                    }
                }
            }
            else if (type == "report")
            {
                JArray header = JArray.Parse(a.Children<JObject>().Properties().FirstOrDefault(z => z.Name == "header").Value.ToString());
                JArray content = JArray.Parse(a.Children<JObject>().Properties().FirstOrDefault(z => z.Name == "content").Value.ToString());
                string[] h1 = header.ToObject<string[]>();
                string[] c1 = content.ToObject<string[]>();
                PrintDocument recordDoc = new PrintDocument();
                recordDoc.DocumentName = "Customer Receipt";
                recordDoc.PrintController = new StandardPrintController(); // hides status dialog popup
                PrinterSettings ps = new PrinterSettings();
                ps.PrinterName = nombre.Value.ToString();
                recordDoc.PrinterSettings = ps;
                recordDoc.PrintPage += (sender, args) => PrintReport(sender, args, h1, c1, a);
                recordDoc.Print();
                recordDoc.Dispose();

            }
            else if (type == "test")
            {

                float x = 4, y = 0, w = 255.59F, h = 0F;
                Font bold_16 = new Font("Arial", 16, FontStyle.Bold);
                SolidBrush drawBrush = new SolidBrush(Color.Black);
                Font regular = new Font("Arial", 8, FontStyle.Regular);

                StringFormat center = new StringFormat();
                center.Alignment = StringAlignment.Center;
                PrintDocument recordDoc = new PrintDocument();
                recordDoc.DocumentName = "Customer Receipt";
                recordDoc.PrintController = new StandardPrintController(); // hides status dialog popup
                PrinterSettings ps = new PrinterSettings();
                ps.PrinterName = nombre.Value.ToString();
                recordDoc.PrinterSettings = ps;
                recordDoc.PrintPage += (sender, args) => imprimir(ref args, "prueba", bold_16, drawBrush, x, ref y, w, h, center);
                recordDoc.Print();
                recordDoc.Dispose();
            }
        }

        public static Image DrawText(String text, Font font, Color textColor, Color backColor)
        {
            //first, create a dummy bitmap just to get a graphics object
            Image img = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(img);

            //measure the string to see how big the image needs to be
            SizeF textSize = drawing.MeasureString(text, font);

            //free up the dummy image and old graphics object
            img.Dispose();
            drawing.Dispose();

            //create a new image of the right size
            img = new Bitmap((int)textSize.Width, (int)textSize.Height);

            drawing = Graphics.FromImage(img);

            //paint the background
            drawing.Clear(backColor);

            //create a brush for the text
            Brush textBrush = new SolidBrush(textColor);

            drawing.DrawString(text, font, textBrush, 0, 0);

            drawing.Save();

            textBrush.Dispose();
            drawing.Dispose();

            return img;
        }

        public static void imprimir(ref PrintPageEventArgs e, string t, Font f, SolidBrush s, float x, ref float y, float w, float h, StringFormat sf)
        {
            e.Graphics.DrawString(t, f, s, new RectangleF(x, y, w, h), sf);
            y += e.Graphics.MeasureString(t, f).Height;
        }

        public static void Coleta(ref PrintPageEventArgs e, float x, ref float y, float w, float h, string serie, string station, string price, string iva, string total)
        {
            Font bold = new Font("Arial", 8, FontStyle.Bold);
            Font bold_16 = new Font("Arial", 16, FontStyle.Bold);
            SolidBrush drawBrush = new SolidBrush(Color.Black);
            Font regular = new Font("Arial", 8, FontStyle.Regular);

            StringFormat center = new StringFormat();
            center.Alignment = StringAlignment.Center;
            StringFormat left = new StringFormat();
            left.Alignment = StringAlignment.Near;
            StringFormat right = new StringFormat();
            right.Alignment = StringAlignment.Far;
            StringFormat align = center;

            Console.WriteLine("coleta");
            string text1 = "";
            //string text1 = "S/C:  " + sc;
            //imprimir(ref e, text1, bold, drawBrush, x, ref y, w, h, left);

            text1 = "Ticket:  " + serie;
            imprimir(ref e, text1, bold, drawBrush, x, ref y, w, h, left);

            text1 = "Taquilla:  " + station;
            imprimir(ref e, text1, bold, drawBrush, x, ref y, w, h, left);
            y -= e.Graphics.MeasureString(text1, regular).Height * 2;

            text1 = "Precio:  " + price;
            imprimir(ref e, text1, bold, drawBrush, x, ref y, w, h, right);

            text1 = "Impuesto:  " + iva;
            imprimir(ref e, text1, bold, drawBrush, x, ref y, w, h, right);

            text1 = "Total a Pagar: " + total;
            imprimir(ref e, text1, bold_16, drawBrush, x, ref y, w, h, center);

            string texto = "Este Boleto es instransferible y sera verificado al momento del ingreso evite molestias";
            imprimir(ref e, texto, regular, drawBrush, x, ref y, w, h, center);
        }

        private static void PrintReport(object sender, PrintPageEventArgs e, string[] h, string[] c, JArray a)
        {

            int mm = e.PageSettings.PaperSize.Width;
            var hijo = a.Children<JObject>().Properties().First();
            if (hijo.Name == "width")
                mm = (int)hijo.Value;
            float x = 10;
            float y = 0;
            float width = ((mm) * (0.039370F) * (100)) - 20;
            float height = 0F;

            StringFormat center = new StringFormat();
            center.Alignment = StringAlignment.Center;
            StringFormat left = new StringFormat();
            left.Alignment = StringAlignment.Near;
            StringFormat right = new StringFormat();
            right.Alignment = StringAlignment.Far;
            StringFormat align = center;

            Font bold_10 = new Font("Arial", 10, FontStyle.Bold);
            Font bold_16 = new Font("Arial", 16, FontStyle.Bold);
            SolidBrush drawBrush = new SolidBrush(Color.Black);

            System.Drawing.Image img = System.Drawing.Image.FromFile("logo.png");
            e.Graphics.DrawImage(img, new Rectangle(40, (int)Math.Ceiling(y), img.Size.Width, img.Size.Height));
            y += img.Size.Height;

            imprimir(ref e, "REPORTE", bold_16, drawBrush, x, ref y, width, height, center);
            y += 10;
            foreach (string text1 in h)
            {
                imprimir(ref e, text1, bold_10, drawBrush, x, ref y, width, height, left);
            }
            foreach (string text1 in c)
            {
                imprimir(ref e, text1, bold_10, drawBrush, x, ref y, width, height, left);
            }
        }

        private static void PrintReceiptPage(object sender, PrintPageEventArgs e, JObject obj, Image img3, JArray a)
        {
            int mm = e.PageSettings.PaperSize.Width;//Ancho por defecto en caso de un mal post
            var hijo = a.Children<JObject>().Properties().First();
            if (hijo.Name == "width")
                mm = (int)hijo.Value;
            float x = 10;
            float y = 0;
            float width = ((mm) * (0.039370F) * (100)) - 20;
            float height = 0F;
            width = width / 2;

            StringFormat center = new StringFormat();
            center.Alignment = StringAlignment.Center;
            StringFormat left = new StringFormat();
            left.Alignment = StringAlignment.Near;
            StringFormat right = new StringFormat();
            right.Alignment = StringAlignment.Far;
            StringFormat align = center;

            Font bold_italic = new Font("Arial", 12, FontStyle.Italic ^ FontStyle.Bold);
            Font bold = new Font("Arial", 8, FontStyle.Bold);
            Font bold_18 = new Font("Arial", 18, FontStyle.Bold);
            Font bold_16 = new Font("Arial", 16, FontStyle.Bold);
            Font bold_14 = new Font("Arial", 14, FontStyle.Bold);
            Font bold_12 = new Font("Arial", 12, FontStyle.Bold);
            Font bold_10 = new Font("Arial", 10, FontStyle.Bold);
            Font regular = new Font("Arial", 8, FontStyle.Regular);
            Font regular_10 = new Font("Arial", 10, FontStyle.Regular);
            Font regular_12 = new Font("Arial", 12, FontStyle.Regular);
            Font regular_16 = new Font("Arial", 16, FontStyle.Regular);
            SolidBrush drawBrush = new SolidBrush(Color.Black);
            SolidBrush blanco = new SolidBrush(Color.White);

            JObject obj2 = JObject.Parse(a.First.ToString());

            string serie = (string)(obj["serie"]);
            string barcode = (string)(obj["barcode"]);
            string qrcode = (string)(obj["serie"]);
            string hour = (string)(obj["hour"]);
            string date = (string)(obj2["date"]);
            string place = (string)(obj2["place"]);
            string evento = (string)(obj2["event"]);
            string zone = (string)(obj2["zone"]);
            string station = (string)(obj2["station"]);
            string price = (string)(obj2["base"]);
            string iva = (string)(obj2["iva"]);
            string total = (string)(obj2["total"]);
            string impuesto = (string)(obj2["impuesto"]);

            if (String.IsNullOrEmpty(qrcode))
            {
                qrcode = "ventickets.com";
            }
            QrEncoder qrEncoder = new QrEncoder(ErrorCorrectionLevel.H);
            QrCode qrCode = new QrCode();
            qrEncoder.TryEncode(qrcode, out qrCode);
            GraphicsRenderer renderer = new GraphicsRenderer(new FixedCodeSize(100, QuietZoneModules.Zero), Brushes.Black, Brushes.White);
            MemoryStream ms = new MemoryStream();

            renderer.WriteToStream(qrCode.Matrix, ImageFormat.Png, ms);
            var img_temp = new Bitmap(ms);
            var qr = new Bitmap(img_temp, new Size(new Point(60, 60)));
            //e.Graphics.DrawImage(qr, new Rectangle(10, (int)Math.Ceiling(y), 60, 60));

            System.Drawing.Image img = System.Drawing.Image.FromFile("logo.png");
            int i = 15;
            y += 10;
            e.Graphics.DrawImage(img, new Rectangle(i, (int)Math.Ceiling(y), img.Size.Width, img.Size.Height));
            string x2 = "00000" + serie;
            e.Graphics.DrawString(date, regular, drawBrush, new RectangleF(width, y, width, height), right);
            float auxH = e.Graphics.MeasureString(date, regular).Height;
            e.Graphics.DrawString(x2, regular, drawBrush, new RectangleF(width, y + auxH, width, height),right);
            e.Graphics.DrawString("N° CONTROL", bold, drawBrush, new RectangleF(width, y + auxH, width-50, height), center);
            y += img.Size.Height;
            if (!String.IsNullOrEmpty(serie)){
                string text2 = "00000" + serie;
                Console.WriteLine(y);
                System.Drawing.Image img_Evento = System.Drawing.Image.FromFile( zone + ".png");
                //e.Graphics.DrawImage(img_Evento, new Rectangle((int)width+4, (int)Math.Ceiling(y), img_Evento.Size.Width-5, img_Evento.Size.Height+1));
                e.Graphics.DrawImage(img_Evento, new Rectangle((int)width+4, (int)Math.Ceiling(y), img_Evento.Size.Width, img_Evento.Size.Height));
                imprimir(ref e, "N° CONTROL", bold_12, drawBrush, x, ref y, width, height, center);
                imprimir(ref e, text2, regular_10, drawBrush, x, ref y, width, height, center);
            }
            if (!String.IsNullOrEmpty(date)){
                imprimir(ref e, date, regular, drawBrush, x, ref y, width, height, center);
            }
            int auxLeft = 5;
            int auxRight = 8;
            y += 10;
            if (!String.IsNullOrEmpty(price)){
                string title = "Base:";
                e.Graphics.DrawString(title, bold_10, drawBrush, new RectangleF(x+auxLeft, y, width, height), left);
                imprimir(ref e, price, regular_12, drawBrush, x, ref y, width-auxRight, height, right);
            }
            if (!String.IsNullOrEmpty(iva)){
                string title = "IVA:";
                e.Graphics.DrawString(title, bold_10, drawBrush, new RectangleF(x+auxLeft, y, width, height), left);
                imprimir(ref e, iva, regular_12, drawBrush, x, ref y, width-auxRight, height, right);
            }
            if (!String.IsNullOrEmpty(impuesto)){
                string title = "Impuesto:";
                e.Graphics.DrawString(title, bold_10, drawBrush, new RectangleF(x+auxLeft, y, width, height), left);
                imprimir(ref e, impuesto, regular_12, drawBrush, x, ref y, width-auxRight, height, right);
            }
            y += 10;
            if (!String.IsNullOrEmpty(total)){
                System.Drawing.Image img_Cuadro = System.Drawing.Image.FromFile("cuadro.png");
                int xAux = 10;
                e.Graphics.DrawImage(img_Cuadro, new Rectangle(xAux, (int)Math.Ceiling(y)-10, img_Cuadro.Size.Width, img_Cuadro.Size.Height));
                imprimir(ref e, total, bold_18, drawBrush, x, ref y, width, height, center);
                imprimir(ref e, "Precio Total", regular_16, drawBrush, x, ref y, width, height, center);
            }
            width = width * 2;
            float yAux = y;
            Console.WriteLine(yAux);
            if (!String.IsNullOrEmpty(evento)){
                System.Drawing.Image img_Back = System.Drawing.Image.FromFile("back.png");
                e.Graphics.DrawImage(img_Back, new Rectangle(0, (int)Math.Ceiling(y), img_Back.Size.Width, img_Back.Size.Height));
                y += 4;
                imprimir(ref e, evento, bold_12, blanco, x, ref y, width, height, center);
            }
            //Coleta(ref e, x, ref y, width, height, serie, station, price, iva, total);
            //y += 15;
            /*if (!String.IsNullOrEmpty(barcode))
            {
                BarcodeLib.Barcode code = new BarcodeLib.Barcode();
                System.Drawing.Image img2 = code.Encode(BarcodeLib.TYPE.CODE128, barcode, Color.Black, Color.White, 400, 50);
                e.Graphics.DrawImage(img2, new Rectangle(0, (int)Math.Ceiling(y), 290, 50));
                y += 60;
            }*/
        }

    }

}


