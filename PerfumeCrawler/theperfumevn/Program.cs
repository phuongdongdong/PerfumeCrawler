﻿using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration;
using SeleniumExtras.WaitHelpers;

namespace TikiCrawler
{
    class Product
    {
        public string Title { get; set; }
        public List<string> Categories { get; set; }
        public string RegularPrice { get; set; }
        public string SalePrice { get; set; }
        public string Description { get; set; }
        public string DetailInformation { get; set; }
        public List<string> ImgUrl { get; set; }
    }
    class Program
    {
        static bool HasSizeInTitle(string title)
        {
            string pattern = @"\s\d+(ml|ML)";
            Match match = Regex.Match(title, pattern);
            return match.Success;
        }
        static decimal CalPrice(int newSize, int fullSize, decimal fullsizePrice)
        {
            decimal ratio = 1.17m + (fullSize / newSize) * 0.002m;
            decimal newPrice = fullsizePrice / fullSize * ratio * newSize;
            return newPrice;
        }
        static Product GetProductData(IWebDriver browser, string productURL)
        {
            try
            {
                browser.Navigate().GoToUrl(productURL);
            }
            catch
            {
                Console.WriteLine("url not found");
            }
            //Declare product information variables
            string productTitle;
            List<string> productCategories = new List<string>();
            List<string> productImgs = new List<string>();
            string productDetails;
            string productPrice;
            string productSalePrice = null;
            string productDescription;

            // Wait for the page to load
            //browser.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            System.Threading.Thread.Sleep(1000);

            //Extract product information by CSS Selector
            try
            {

                productTitle = browser.FindElement(By.CssSelector("h1.product-title")).Text;
                //check if product is a size variation of some parent product
                if (HasSizeInTitle(productTitle))
                {
                    return null;
                }
                Console.WriteLine("Product title: " + productTitle);
            }
            catch
            {
                Console.WriteLine("Title not found");
                return null;
            }

            //breadcrumb store brand and category information
            var breadcrumb = browser.FindElement(By.CssSelector(".breadcrumbs"));
            //get product category
            string productCategory = "";
            try
            {
                productCategory = breadcrumb.FindElement(By.CssSelector("a:nth-of-type(3)")).Text;
                if (productCategory == "Nước hoa nam")
                {
                    productCategory = "Dành cho nam";
                }
                else
                {
                    productCategory = "Dành cho nữ";
                }
                productCategories.Add(productCategory);
                Console.WriteLine("Product category: " + productCategory);
            }
            catch
            {
                Console.WriteLine("Category not found");
                return null;
            }

            //get product brand
            try
            {
                string productBrand = breadcrumb.FindElement(By.CssSelector("a:nth-of-type(4)")).Text;
                TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
                productBrand = textInfo.ToTitleCase(productBrand.ToLower());
                if (productCategory != "")
                {
                    //save as format of subcategory: for men/women > brand
                    productCategories.Add(productCategory + " > " + productBrand);
                }
                Console.WriteLine("Product brand: " + productBrand);
            }
            catch
            {
                Console.WriteLine("Brand not found");
            }


            var groupImgs = browser.FindElements(By.CssSelector("img.skip-lazy"));
            //Extract product images
            foreach (var img in groupImgs)
            {
                try
                {
                    string imgSrc = img.GetAttribute("src");
                    //Get bigger img from img cdn
                    string pattern = @"^(.*)-\d+x\d+(\.\w+)$";
                    Regex regex = new Regex(pattern);
                    string newImageUrl = regex.Replace(imgSrc, $"{regex.Match(imgSrc).Groups[1].Value}{regex.Match(imgSrc).Groups[2].Value}");
                    productImgs.Add(newImageUrl);
                    Console.WriteLine("Image source: " + newImageUrl);
                }
                catch
                {
                    Console.WriteLine("Image not found");
                }
            }
            if (productImgs.Count == 0)
                return null;




            ////Extract product price
            //try
            //{
            //    string currentPrice = browser.FindElement(By.CssSelector(".product-price__current-price")).Text;
            //    currentPrice = Regex.Match(currentPrice, "^[\\d|\\.|\\,]+").Value;

            //    try //have both listPrice and currentPrice
            //    {
            //        string listPrice = browser.FindElement(By.CssSelector(".product-price__list-price")).Text;
            //        listPrice = Regex.Match(listPrice, "^[\\d|\\.|\\,]+").Value;
            //        //currentPrice is sale price
            //        productSalePrice = currentPrice.Replace(".", string.Empty);
            //        Console.WriteLine("Product sale price: " + productSalePrice);
            //        //listPrice is regular price
            //        productPrice = listPrice.Replace(".", string.Empty);
            //        Console.WriteLine("Product price: " + productPrice);
            //    }
            //    catch //have only current price
            //    {
            //        productPrice = currentPrice.Replace(".", string.Empty);
            //        Console.WriteLine("Product price: " + productPrice);
            //        Console.WriteLine("Product do not sale");
            //    }
            //}
            //catch
            //{
            //    Console.WriteLine("Price not found");
            //    return null;
            //}


            //Extract product details
            try
            {
                productDetails = browser.FindElement(By.CssSelector(".product-short-description")).GetAttribute("innerHTML");
                //Console.WriteLine(productDetails);
            }
            catch
            {
                Console.WriteLine("Details not found");
                return null;
            }

            //Extract product description
            try
            {
                //string stars = browser.FindElement(By.CssSelector("#tab-description>div:last-child")).GetAttribute("outerHTML");
                //string writer = browser.FindElement(By.CssSelector("#tab-description>p:last-of-type")).GetAttribute("outerHTML");
                //productDescription = browser.FindElement(By.CssSelector("#tab-description")).GetAttribute("innerHTML");
                //productDescription = productDescription.Remove(productDescription.Length - stars.Length);
                //productDescription = productDescription.Remove(productDescription.Length - writer.Length - 10);
                //Console.WriteLine(productDescription);

                var productDescriptions = browser.FindElements(By.CssSelector("#tab-description>*:not(#tab-description>div:last-child, #tab-description>p:last-of-type)"));
                productDescription = "";
                foreach (var ele in productDescriptions)
                {
                    productDescription += ele.GetAttribute("outerHTML");
                }
                //Console.WriteLine(productDescription);
            }
            catch
            {
                Console.WriteLine("Description not found");
                return null;
            }

            //Extract price
            string price;
            try
            {
                try     //product is on sale, get the price being deleted
                {
                    price = browser.FindElement(By.CssSelector(".product-page-price del bdi")).Text;
                    price = price.Remove(price.Length - 2);
                    price = price.Replace(".", "");
                    Console.WriteLine(price);
                }
                catch   //product have price rance, get the last price for fullsize
                {
                    price = browser.FindElement(By.CssSelector(".product-page-price span:last-child bdi")).Text;
                    //remove " đ"
                    price = price.Remove(price.Length - 2);
                    //remove number format with .
                    price = price.Replace(".", "");
                    Console.WriteLine(price);
                }
            }
            catch   // product is out of stock and don't have price
            {
                return null;
            }
            

            string fullsize;
            try
            {
                var select = browser.FindElement(By.CssSelector("#pa_dung-tich"));
                var options = select.FindElements(By.TagName("option"));
                fullsize = options[options.Count - 1].GetAttribute("value");
                //remove "ml"
                fullsize = fullsize.Remove(fullsize.Length - 2);
                Console.WriteLine(fullsize);
            }
            catch
            {
                fullsize = "100";
            }
            // convert price to decimal
            decimal fullsizePrice;
            try
            {
                fullsizePrice = Convert.ToDecimal(price);
            }
            catch
            {
                Console.WriteLine(price);
                return null;
            }
            //convert size to int
            int fullsizeValue;
            try
            {
                fullsizeValue = Convert.ToInt32(fullsize);
            }
            catch
            {
                Console.WriteLine(price);
                return null;
            }
            decimal size10mlPrice = CalPrice(10, fullsizeValue, fullsizePrice);
            decimal size20mlPrice = CalPrice(20, fullsizeValue, fullsizePrice);
            decimal size30mlPrice = CalPrice(30, fullsizeValue, fullsizePrice);

            if (productDetails.Contains("mát"))
            {
                decimal saleRate = 0.1m;
                decimal size10mlSalePrice = size10mlPrice * (1 - saleRate);
                decimal size20mlSalePrice = size20mlPrice * (1 - saleRate);
                decimal size30mlSalePrice = size30mlPrice * (1 - saleRate);
                productSalePrice = (fullsizePrice * (1 - saleRate)).ToString();
                Console.WriteLine("product is on sale");
                productCategories.Add("Đang khuyến mãi");
            }
            //Create product object from product informations collected
            var product = new Product { Title = productTitle, Categories = productCategories, ImgUrl = productImgs, Description = productDescription, DetailInformation = productDetails, RegularPrice = fullsizePrice.ToString(), SalePrice = productSalePrice };
            //Product product = new Product();
            return product;
        }
        static void Export(List<Product> productsData)
        {
            //Config delimiter
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = "@"
            };
            using (var writer = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\products.csv"))
            using (var csv = new CsvWriter(writer, config))
            {
                //Write data header
                csv.WriteField("Name");
                csv.WriteField("Categories");
                csv.WriteField("Regular Price");
                csv.WriteField("Sale Price");
                csv.WriteField("Images");
                csv.WriteField("Description");
                csv.WriteField("Short Description");
                csv.NextRecord();

                // Write the data rows
                foreach (var product in productsData)
                {
                    csv.WriteField(product.Title);
                    csv.WriteField(string.Join(",", product.Categories));
                    csv.WriteField(product.RegularPrice);
                    csv.WriteField(product.SalePrice);
                    csv.WriteField(string.Join(",", product.ImgUrl));
                    csv.WriteField(product.Description);
                    csv.WriteField(product.DetailInformation);
                    csv.NextRecord();
                }

            }
        }
        static void Main(string[] args)
        {
            //Define total number of product needed to get
            int totalProductCount = 30;

            //Create an instance of Chrome driver
            IWebDriver browser = new ChromeDriver();

            //store product crawled
            var productsData = new List<Product>();

            browser.Navigate().GoToUrl("https://theperfume.vn/thuong-hieu-nuoc-hoa/");
            var brands = browser.FindElements(By.CssSelector(".wb-thumb-title a"));
            List<string> brandURLs = new List<string>();
            foreach (var brand in brands)
            {
                string brandURL = brand.GetAttribute("href");
                brandURLs.Add(brandURL);
            }
            //go to each brand
            foreach(var brandURL in brandURLs)
            {
                if (productsData.Count >= totalProductCount)
                    break;
                browser.Navigate().GoToUrl(brandURL);
                //first page of that brand
                int pageIndex = 1;
                //use while as we don't know how many pages
                while (productsData.Count < totalProductCount)
                {
                    //select all to product in page
                    var productURLElements = browser.FindElements(By.CssSelector(".products .product-title a"));
                    List<string> productURLs = new List<string>();
                    //no product left on this brand, move to next brand
                    if (productURLElements.Count == 0)
                        break;
                    //extract products url
                    foreach (var productURLElement in productURLElements)
                    {
                        string productURL = productURLElement.GetAttribute("href");
                        productURLs.Add(productURL);
                    }
                    //go to each product
                    foreach (string productURL in productURLs)
                    {
                        if (productsData.Count >= totalProductCount)
                            break;
                        Product productData = GetProductData(browser, productURL);
                        if (productData != null)
                            productsData.Add(productData);
                    }
                    //not get enough products, go to next page
                    if (productsData.Count < totalProductCount)
                    {
                        browser.Navigate().GoToUrl(brandURL + $"page/{++pageIndex}/");
                    }
                }
                

            }



            //https://theperfume.vn/nuoc-hoa/nuoc-hoa-givenchy-play-intense/

            Export(productsData);

        }
    }
}