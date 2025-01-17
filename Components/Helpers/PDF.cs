﻿using CustomersManagementApp.Components.DataContext;
using CustomersManagementApp.Components.Entities;

using iTextSharp.text;
using iTextSharp.text.pdf;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CustomersManagementApp.Components.Helpers {
	public class PDF
    {
        private Invoice invoice = null;
        private Customer customer = null;
        private List<InvoiceItem> invoiceItems = null;

        private readonly InvoiceContext _context;
        private Settings settings;

        public PDF(InvoiceContext context)
        {
            _context = context;
            this.GetSettings();
        }

        private void GetSettings()
        {
            this.settings = this._context.Settings.FirstOrDefault();
        }

        private async Task GetData(string id)
        {
            try
            {
                invoice = await _context.Invoices.FirstOrDefaultAsync(s => s.InvoiceNumber == id);
                customer = await _context.Customers.Include(i => i.Addresses).FirstOrDefaultAsync(s => s.Id == invoice.CustomerId);
                invoiceItems = await _context.InvoiceItems.Where(s => s.InvoiceNumber == invoice.InvoiceNumber).ToListAsync();
                customer.Addresses.First().Address = await _context.Addresses.FirstOrDefaultAsync(q => q.PostalCode == customer.Addresses.First().PostalCode && q.Number == customer.Addresses.First().Number);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public async Task<byte[]> CreatePDF(string id)
        {
            await this.GetData(id);

            var invoiceNumber = invoice.InvoiceNumber.ToString();

            //Creating PDF document
            var doc = this.InitDocument();
            MemoryStream ms = new MemoryStream();
            PdfWriter writer = PdfWriter.GetInstance(doc, ms);

            doc.Open();
            PdfContentByte cb = writer.DirectContent;

            // Fonts
            BaseFont helvetica = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            BaseFont helveticaBold = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

            // Company logo
            if (settings.ShowLogoInPDF)
            {
                // Convert base64 string to byte[]
                byte[] imageBytes = Convert.FromBase64String(settings.Logo.Split(',')[1]);

                // Convert byte[] to Image
                System.Drawing.Image img = null;
                using (var ims = new MemoryStream(imageBytes, 0, imageBytes.Length))
                {
                    img = System.Drawing.Image.FromStream(ims, true);
                }

                // Get file format
                var format = ImageFormat.Jpeg;
                if (ImageFormat.Jpeg.Equals(img.RawFormat))
                {
                    format = ImageFormat.Jpeg;
                }
                else if (ImageFormat.Png.Equals(img.RawFormat))
                {
                    format = ImageFormat.Png;
                }
                else if (ImageFormat.Gif.Equals(img.RawFormat))
                {
                    format = ImageFormat.Gif;
                }

                // Position logo
                var maxWidth = 220f;
                var maxHeight = 150f;
                var margin = 30f;
                var logo = Image.GetInstance(img, format);

                logo.ScaleToFit(maxWidth, maxHeight);
                logo.Alignment = Image.LEFT_ALIGN;
                logo.SetAbsolutePosition(45, (doc.PageSize.Height - margin) - logo.ScaledHeight);

                doc.Add(logo);
            }

            // Company info
            cb.BeginText();
            cb.SetFontAndSize(helveticaBold, 12f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, settings.CompanyName, 430, 795, 0);                               //795
            cb.SetFontAndSize(helvetica, 9.5f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, settings.Address, 430, 780, 0);                                  //780
            cb.ShowTextAligned(Element.ALIGN_LEFT, settings.PostalCode + ", " + settings.City, 430, 765, 0);        //765
            cb.ShowTextAligned(Element.ALIGN_LEFT, settings.Email, 430, 745, 0);                                    //745
            cb.ShowTextAligned(Element.ALIGN_LEFT, settings.Phone, 430, 730, 0);                                    //730
            cb.ShowTextAligned(Element.ALIGN_LEFT, "COC: " + settings.BusinessNumber, 430, 710, 0);                 //710
            cb.ShowTextAligned(Element.ALIGN_LEFT, "VAT: " + settings.VAT, 430, 695, 0);                            //695
            cb.ShowTextAligned(Element.ALIGN_LEFT, "IBAN: " + settings.BankAccount, 430, 680, 0);                   //680
            cb.EndText();

            // Customer name and address
            var customerAddress = customer.Addresses.First().Address;

            cb.BeginText();
            cb.SetFontAndSize(helvetica, 10f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, customer.FirstName + " " + customer.LastName, 70, 635, 0);
            cb.ShowTextAligned(Element.ALIGN_LEFT, customerAddress.Street + " " + customerAddress.Number + customerAddress.Suffix, 70, 620, 0);
            cb.ShowTextAligned(Element.ALIGN_LEFT, customerAddress.PostalCode + " " + customerAddress.City, 70, 605, 0);
            cb.EndText();

            // Invoice number
            cb.BeginText();
            cb.SetFontAndSize(helveticaBold, 15f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, "Invoice " + settings.InvoicePrefix + invoice.InvoiceNumber, 55, 500, 0);
            cb.EndText();

            // Invoice dates
            cb.BeginText();
            cb.SetFontAndSize(helvetica, 10f);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "Invoice date:       " + invoice.CreatedOn.ToString("dd-MM-yyyy"), 550, 500, 0);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "Expiration date:       " + invoice.ExpiredOn.ToString("dd-MM-yyyy"), 550, 485, 0);
            cb.EndText();

            // Invoice items subjects
            cb.BeginText();
            cb.SetFontAndSize(helvetica, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, "Item", 48, 452, 0);
            cb.ShowTextAligned(Element.ALIGN_LEFT, "Description", 185, 452, 0);
            cb.ShowTextAligned(Element.ALIGN_LEFT, "Price", 350, 452, 0);
            cb.ShowTextAligned(Element.ALIGN_LEFT, "Qty.", 424, 452, 0);
            cb.ShowTextAligned(Element.ALIGN_LEFT, "Total", 470, 452, 0);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "VAT", 560, 452, 0);
            cb.EndText();

            cb.Rectangle(45, 445, 520, 1);
            cb.SetRgbColorStroke(75, 75, 75);
            cb.Stroke();

            // Place invoice item info
            decimal subtotal = 0;
            decimal taxTotal = 0;

            var y = 435;
            foreach (var item in invoiceItems)
            {
                cb.SetFontAndSize(helvetica, 9f);
                cb.BeginText();

                // Item
                cb.ShowTextAligned(Element.ALIGN_LEFT, item.Name, 48, y, 0);

                // Desc
                cb.ShowTextAligned(Element.ALIGN_LEFT, item.Description, 185, y, 0);

                //  Price
                cb.ShowTextAligned(Element.ALIGN_LEFT, "€" + item.Price.ToString("N2"), 350, y, 0);

                //  Quantity
                cb.ShowTextAligned(Element.ALIGN_LEFT, item.Quantity.ToString(), 425, y, 0);

                // Total
                var total = item.Price * item.Quantity;
                cb.ShowTextAligned(Element.ALIGN_LEFT, "€" + total.ToString("N2"), 470, y, 0);

                // VAT
                cb.ShowTextAligned(Element.ALIGN_RIGHT, item.Tax + "%", 560, y, 0);

                cb.EndText();

                // Divider
                if (item != invoiceItems.Last())
                {
                    cb.SetRgbColorStroke(170, 170, 170);
                    cb.Rectangle(48, y - 5, 517, 0.20f);
                    cb.Stroke();
                }

                y = y - 15;

                var vatAmount = (total / 100) * item.Tax;

                taxTotal += vatAmount;
                subtotal += (total - vatAmount);
            }

            y = y - 50;

            cb.BeginText();

            // Subtotal
            cb.SetFontAndSize(helvetica, 10f);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "Subtotal", 390, y, 0);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "€ " + subtotal.ToString("N2"), 509, y, 0);

            // Total VAT
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "Amount VAT", 390, y - 15, 0);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "€ " + taxTotal.ToString("N2"), 509, y - 15, 0);

            // Divider
            cb.SetRgbColorStroke(170, 170, 170);
            cb.Rectangle(310, y - 21, 200, 0.20f);
            cb.Stroke();

            // Discount
            cb.SetFontAndSize(helveticaBold, 10f);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "Discount", 390, y - 34, 0);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "€ " + invoice.Discount.ToString("N2"), 509, y - 34, 0);

            // Total
            cb.SetFontAndSize(helveticaBold, 10f);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "Total", 390, y - 49, 0);
            cb.ShowTextAligned(Element.ALIGN_RIGHT, "€ " + invoice.Total.ToString("N2"), 509, y - 49, 0);

            cb.EndText();

            // Disclaimer
            var disclaimerText = "We kindly request you to pay the amount of";
            var disclaimerTotal = String.Format("€{0:N2}", invoice.Total.ToString("N2"));
            var disclaimerText2 = "described above before";
            var disclaimerDate = invoice.ExpiredOn.ToString("dd-MM-yyyy");

            var disclaimerText3 = "on our bank account";
            var disclaimerBankAccount = settings.BankAccount;
            var disclaimerText4 = "in the name of";
            var disclaimerCompany = settings.CompanyName + ",";
            var disclaimerText5 = "indicating the invoice number";
            var disclaimerInvoice = invoiceNumber + ".";
            var disclaimerText6 = "For questions, please contact.";

            cb.BeginText();
            cb.SetFontAndSize(helvetica, 9f);

            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerText, 110, 50, 0);
            cb.SetFontAndSize(helveticaBold, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerTotal, 290, 50, 0);
            cb.SetFontAndSize(helvetica, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerText2, 345, 50, 0);
            cb.SetFontAndSize(helveticaBold, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerDate, 441, 50, 0);
            cb.SetFontAndSize(helvetica, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerText3, 150, 38, 0);
            cb.SetFontAndSize(helveticaBold, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerBankAccount, 236, 38, 0);
            cb.SetFontAndSize(helvetica, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerText4, 350, 38, 0);
            cb.SetFontAndSize(helveticaBold, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerCompany, 410, 38, 0);
            cb.SetFontAndSize(helvetica, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerText5, 150, 26, 0);
            cb.SetFontAndSize(helveticaBold, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerInvoice, 270, 26, 0);
            cb.SetFontAndSize(helvetica, 9f);
            cb.ShowTextAligned(Element.ALIGN_LEFT, disclaimerText6, 313, 26, 0);
            cb.EndText();

            // Close and return pdf
            doc.Close();
            return ms.ToArray();
        }

        private Document InitDocument()
        {
            Document doc = new Document(PageSize.A4, 30, 30, 42, 42);
            doc.AddSubject(String.Format("This invoice belongs to {0}. {1}", customer.FirstName[0], customer.LastName));
            doc.AddKeywords("Invoice, Payment");
            doc.AddCreator(this.settings.CompanyName);

            return doc;
        }
    }
}
