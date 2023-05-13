using OpenQA.Selenium;
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
        public string Type { get; set; }
        public string SKU { get; set; }
        public List<string> Categories { get; set; }
        public string Parent { get; set; }
        public string RegularPrice { get; set; }
        public string SalePrice { get; set; }
        public string Description { get; set; }
        public string DetailInformation { get; set; }
        public string TaxClass { get; set; }
        public int InStock { get; set; }
        public int? Stock { get; set; }
        public List<string> Tags { get; set; }
        public List<string> ImgUrl { get; set; }
        public string AttributeName { get; set; }
        public string AttributeValue { get; set; }
        public int AttributeVisible { get; set; }
        public int AttributeGlobal { get; set; }
    }
    class Program
    {
        static bool HasSizeInTitle(string title)
        {
            string pattern = @"\s\d+(ml|ML)";
            Match match = Regex.Match(title, pattern);
            return match.Success;
        }
        static bool HasSizeInURL(string url)
        {
            string pattern = @"-\d+(ml|ML)";
            Match match = Regex.Match(url, pattern);
            return match.Success;
        }
        static decimal CalPrice(int newSize, int fullSize, decimal fullsizePrice)
        {
            decimal ratio = 1.17m + (fullSize / newSize) * 0.002m;
            decimal newPrice = fullsizePrice / fullSize * ratio * newSize;
            return newPrice;
        }
        static List<Product> GetProductData(IWebDriver browser, string productURL)
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
            string productType;
            string productSKU = "";
            List<string> productCategories = new List<string>();
            string productParent;
            List<string> productImgs = new List<string>();
            string productDetails;
            string productPrice;
            string productSalePrice = null;
            string productDescription;
            int productStock;
            List<string> productTags = new List<string>();
            string attributeName = "Size";



            List<Product> listVariableProduct = new List<Product>();

            // Wait for the page to load
            //browser.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            System.Threading.Thread.Sleep(1000);

            //Extract product information by CSS Selector
            try
            {
                productTitle = browser.FindElement(By.CssSelector("h1.product-title")).Text;
                //check if product is a size variation of some parent product
                //if (HasSizeInTitle(productTitle))
                //{
                //    return null;
                //}
                Console.WriteLine("Product title: " + productTitle);
            }
            catch
            {
                Console.WriteLine("Title not found");
                return null;
            }

            //Define product type
            productType = "variable";

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
                productCategory = "Uncategorized";
            }

            //get product brand
            string productBrand = "";
            try
            {
                productBrand = breadcrumb.FindElement(By.CssSelector("a:nth-of-type(4)")).Text;
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

            //If product have no brand, no category
            if (productCategories.Count == 0)
            {
                productCategories.Add("Uncategorized");
            }
            //create product sku
            string titlePart = productTitle.Replace("Nước hoa ", "").Replace(" ", "");
            if (productCategory != "Uncategorized")
            {
                if (productCategory == "Dành cho nam")
                {
                    //Men Perfume
                    productSKU = "MP";
                }
                else
                {
                    //Women Perfume
                    productSKU = "WP";
                }
            }
            else
                //Uncategorized Perfume
                productSKU = "UP";
            productSKU += "-" + titlePart;
            if (productBrand != "")
            {
                productSKU +=  "-" + productBrand;
            }


            //Extract product images
            var groupImgs = browser.FindElements(By.CssSelector("img.skip-lazy"));
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

            //set stock quantity
            var rand = new Random();
            //stock = random from 1 to 20
            int fullSizeStock = rand.Next(21) + 1;
            int size10mlStock = rand.Next(21) + 1;
            int size20mlStock = rand.Next(21) + 1;
            int size30mlStock = rand.Next(21) + 1;
            productStock = fullSizeStock + size10mlStock + size20mlStock + size30mlStock;

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
                catch   //product have price range, get the last price for fullsize
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

            //Rounding price of variants
            size10mlPrice = Math.Floor(size10mlPrice / 1000) * 1000;
            size20mlPrice = Math.Floor(size20mlPrice / 1000) * 1000;
            size30mlPrice = Math.Floor(size30mlPrice / 1000) * 1000;


            //Get tags
            var tagLinks = browser.FindElements(By.CssSelector(".tagged_as a"));
            foreach( var tagLink in tagLinks)
            {
                productTags.Add(tagLink.Text);
                Console.WriteLine("tag: " + tagLink.Text);
            }

            string attributeValues = "10ml, 20ml, 30ml, " + fullsizeValue + "ml";

            //Create product object from product informations collected
            var product = new Product { Title = productTitle, Type = productType, SKU = productSKU, Categories = productCategories, ImgUrl = productImgs, Description = productDescription, DetailInformation = productDetails, InStock = 1, Stock = null, RegularPrice = fullsizePrice.ToString(), SalePrice = productSalePrice, Tags = productTags, AttributeName = attributeName, AttributeValue = attributeValues, AttributeVisible = 1, AttributeGlobal = 0 };
            listVariableProduct.Add(product);


            //Declare variation product information variables
            string VProductTitle;
            string VProductType;
            string VProductParent;
            string VProductPrice;
            string VProductSalePrice;
            string VProductTaxClass;

            //Calculate sale price for product contains "mát" and have high stock level
            string? size10mlSalePrice = null;
            string? size20mlSalePrice = null;
            string? size30mlSalePrice = null;

            if (productDetails.Contains("mát") || productStock > 30)
            {
                decimal saleRate = 0.1m;
                size10mlSalePrice = (Math.Floor((size10mlPrice * (1 - saleRate)) / 1000 ) * 1000).ToString();
                size20mlSalePrice = (Math.Floor((size20mlPrice * (1 - saleRate)) / 1000 ) * 1000).ToString();
                size30mlSalePrice = (Math.Floor((size30mlPrice * (1 - saleRate)) / 1000 ) * 1000).ToString();
                productSalePrice = (Math.Floor((fullsizePrice * (1 - saleRate)) / 1000 ) * 1000).ToString();

                Console.WriteLine("product is on sale");
                productCategories.Add("Đang khuyến mãi");
            }

            //Fullsize variation product
            VProductTitle = productTitle + " - " + fullsizeValue + "ml";
            VProductType = "variation";
            VProductParent = productSKU;
            VProductPrice = fullsizePrice.ToString();
            VProductSalePrice= productSalePrice;
            VProductTaxClass = "parent";

            //Create product object from full size variation product informations collected
            var productFullSize = new Product { Title = VProductTitle, Type = VProductType, Categories = productCategories, ImgUrl = productImgs, RegularPrice = VProductPrice.ToString(), SalePrice = VProductSalePrice, Parent = productSKU, TaxClass = VProductTaxClass, InStock = 1, Stock = fullSizeStock, Tags = productTags, AttributeName = attributeName, AttributeValue = fullsizeValue + "ml", AttributeGlobal = 0 };
            //Add to product list
            listVariableProduct.Add(productFullSize);


            //10ml variation product
            VProductTitle = productTitle + " - 10ml";
            VProductType = "variation";
            VProductParent = productSKU;
            VProductPrice = size10mlPrice.ToString();
            VProductSalePrice = size10mlSalePrice;

            //Create product object from 10ml variation product informations collected
            var product10ml = new Product { Title = VProductTitle, Type = VProductType, Categories = productCategories, ImgUrl = productImgs, RegularPrice = VProductPrice.ToString(), SalePrice = VProductSalePrice, Parent = productSKU, TaxClass = VProductTaxClass, InStock = 1, Stock = size10mlStock, Tags = productTags, AttributeName = attributeName, AttributeValue = "10ml", AttributeGlobal = 0 };
            //Add to product list
            listVariableProduct.Add(product10ml);


            //20ml variation product
            VProductTitle = productTitle + " - 20ml";
            VProductType = "variation";
            VProductParent = productSKU;
            VProductPrice = size20mlPrice.ToString();
            VProductSalePrice= size20mlSalePrice;

            //Create product object from 20ml variation product informations collected
            var product20ml = new Product { Title = VProductTitle, Type = VProductType, Categories = productCategories, ImgUrl = productImgs, RegularPrice = VProductPrice.ToString(), SalePrice = VProductSalePrice, Parent = productSKU, TaxClass = VProductTaxClass, InStock = 1, Stock = size20mlStock, Tags = productTags, AttributeName = attributeName, AttributeValue = "20ml", AttributeGlobal = 0 };
            //Add to product list
            listVariableProduct.Add(product20ml);


            //30ml variation product
            VProductTitle = productTitle + " - 30ml";
            VProductType = "variation";
            VProductParent = productSKU;
            VProductPrice = size30mlPrice.ToString();
            VProductSalePrice = size30mlSalePrice;

            //Create product object from 30ml variation product informations collected
            var product30ml = new Product { Title = VProductTitle, Type = VProductType, Categories = productCategories, ImgUrl = productImgs, RegularPrice = VProductPrice.ToString(), SalePrice = VProductSalePrice, Parent = productSKU, TaxClass = VProductTaxClass, InStock = 1, Stock = size30mlStock, Tags = productTags, AttributeName = attributeName, AttributeValue = "30ml", AttributeGlobal = 0 };
            //Add to product list
            listVariableProduct.Add(product30ml);

            return listVariableProduct;
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
                csv.WriteField("SKU");
                csv.WriteField("Type");
                csv.WriteField("Categories");
                csv.WriteField("Parent");
                csv.WriteField("Regular Price");
                csv.WriteField("Sale Price");
                csv.WriteField("Images");
                csv.WriteField("Description");
                csv.WriteField("Short Description");
                csv.WriteField("In stock?");
                csv.WriteField("Stock");
                csv.WriteField("Tags");
                csv.WriteField("Attribute 1 Name");
                csv.WriteField("Attribute 1 Value(s)");
                csv.WriteField("Attribute 1 Visible");
                csv.WriteField("Attribute 1 Global");
                csv.NextRecord();

                // Write the data rows
                foreach (var product in productsData)
                {
                    csv.WriteField(product.Title);
                    csv.WriteField(product.SKU);
                    csv.WriteField(product.Type);
                    csv.WriteField(string.Join(",", product.Categories));
                    csv.WriteField(product.Parent);
                    csv.WriteField(product.RegularPrice);
                    csv.WriteField(product.SalePrice);
                    csv.WriteField(string.Join(",", product.ImgUrl));
                    csv.WriteField(product.Description);
                    csv.WriteField(product.DetailInformation);
                    csv.WriteField(product.InStock);
                    csv.WriteField(product.Stock);
                    csv.WriteField(string.Join(",", product.Tags));
                    csv.WriteField(product.AttributeName);
                    csv.WriteField(product.AttributeValue);
                    csv.WriteField(product.AttributeVisible);
                    csv.WriteField(product.AttributeGlobal);
                    csv.NextRecord();
                }

            }
        }
        static void Main(string[] args)
        {
            //Define total number of product needed to get
            //Each product have 4 variations
            int totalProductCount = 1;

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
                        if (!HasSizeInURL(productURL))
                            productURLs.Add(productURL);
                    }
                    //go to each product
                    foreach (string productURL in productURLs)
                    {
                        if (productsData.Count >= totalProductCount)
                            break;
                        List<Product> productData = GetProductData(browser, productURL);
                        if (productData != null)
                            productsData.AddRange(productData);
                    }
                    //not get enough products, go to next page
                    if (productsData.Count < totalProductCount)
                    {
                        browser.Navigate().GoToUrl(brandURL + $"page/{++pageIndex}/");
                    }
                }
                

            }

            Export(productsData);

        }
    }
}