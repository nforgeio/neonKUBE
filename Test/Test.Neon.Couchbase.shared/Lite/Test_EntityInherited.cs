//-----------------------------------------------------------------------------
// FILE:	    Test_EntityInherited.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Couchbase;
using Couchbase.Lite;
using Couchbase.Lite.Auth;

using Neon.Common;
using Neon.DynamicData;
using Neon.DynamicData.Internal;
using Neon.Xunit;

using Xunit;

using Test.Neon.Models;

namespace TestLiteExtensions
{
    /// <summary>
    /// Verify that inherited entities work correctly.
    /// </summary>
    public class Test_EntityInherited
    {
        public Test_EntityInherited()
        {
            // We need to make sure all generated entity 
            // classes have been registered.

            ModelTypes.Register();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void EntityTypeProperty()
        {
            // Verify that the special entity type properties return the 
            // correct values.

            Assert.Equal(ProductTypes.Product, new Product().ProductType);
            Assert.Equal(ProductTypes.Candy, new Candy().ProductType);
            Assert.Equal(ProductTypes.Gum, new Gum().ProductType);
            Assert.Equal(ProductTypes.CandyBar, new CandyBar().ProductType);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void KnownTypes()
        {
            // Verify that we can read/write derived entities with known types.

            using (var test = new TestDatabase())
            {
                var db = test.Database;

                //-------------------------------

                var productDoc = db.GetEntityDocument<Product>("product");

                productDoc.Content.Name = "Computer";
                Assert.Equal(ProductTypes.Product, productDoc.Content.ProductType);
                Assert.Equal("product", productDoc.Type);
                Assert.Equal("Computer", productDoc.Content.Name);

                productDoc.Save();
                productDoc = db.GetEntityDocument<Product>("product");

                Assert.Equal(ProductTypes.Product, productDoc.Content.ProductType);
                Assert.Equal("product", productDoc.Type);
                Assert.Equal("Computer", productDoc.Content.Name);

                //-------------------------------

                var candyDoc = db.GetEntityDocument<Candy>("candy");

                candyDoc.Content.Name = "candy";
                candyDoc.Content.Calories = 100;
                Assert.Equal(ProductTypes.Candy, candyDoc.Content.ProductType);
                Assert.Equal("product.candy", candyDoc.Type);
                Assert.Equal("candy", candyDoc.Content.Name);
                Assert.Equal(100, candyDoc.Content.Calories);

                candyDoc.Save();
                candyDoc = db.GetEntityDocument<Candy>("candy");

                Assert.Equal(ProductTypes.Candy, candyDoc.Content.ProductType);
                Assert.Equal("product.candy", candyDoc.Type);
                Assert.Equal("candy", candyDoc.Content.Name);
                Assert.Equal(100, candyDoc.Content.Calories);

                //-------------------------------

                var gumDoc = db.GetEntityDocument<Gum>("gum");

                gumDoc.Content.Name = "gum";
                gumDoc.Content.Calories = 1;
                gumDoc.Content.Flavor = "spearmint";
                Assert.Equal(ProductTypes.Gum, gumDoc.Content.ProductType);
                Assert.Equal("product.candy.gum", gumDoc.Type);
                Assert.Equal("gum", gumDoc.Content.Name);
                Assert.Equal(1, gumDoc.Content.Calories);
                Assert.Equal("spearmint", gumDoc.Content.Flavor);

                gumDoc.Save();
                gumDoc = db.GetEntityDocument<Gum>("gum");

                Assert.Equal(ProductTypes.Gum, gumDoc.Content.ProductType);
                Assert.Equal("product.candy.gum", gumDoc.Type);
                Assert.Equal("gum", gumDoc.Content.Name);
                Assert.Equal(1, gumDoc.Content.Calories);
                Assert.Equal("spearmint", gumDoc.Content.Flavor);

                //-------------------------------
                // Read [Candy] as [Product]

                productDoc = db.GetEntityDocument<Product>("candy");

                Assert.Equal("product.candy", productDoc.Type);
                Assert.Equal(ProductTypes.Candy, productDoc.Content.ProductType);
                Assert.Equal("candy", candyDoc.Content.Name);

                var candy = (Candy)productDoc.Content;

                Assert.Equal("product.candy", productDoc.Type);
                Assert.Equal(ProductTypes.Candy, candy.ProductType);
                Assert.Equal("candy", candy.Name);
                Assert.Equal(100, candy.Calories);

                //-------------------------------
                // Read [Gum] as [Product]

                productDoc = db.GetEntityDocument<Product>("gum");

                Assert.Equal("product.candy.gum", productDoc.Type);
                Assert.Equal(ProductTypes.Gum, productDoc.Content.ProductType);
                Assert.Equal("gum", productDoc.Content.Name);

                var gum = (Gum)productDoc.Content;

                Assert.Equal("product.candy.gum", gumDoc.Type);
                Assert.Equal(ProductTypes.Gum, gum.ProductType);
                Assert.Equal("gum", gum.Name);
                Assert.Equal(1, gum.Calories);
                Assert.Equal("spearmint", gum.Flavor);

                //-------------------------------
                // Read [Gum] as [Candy]

                candyDoc = db.GetEntityDocument<Candy>("gum");

                Assert.Equal("product.candy.gum", candyDoc.Type);
                Assert.Equal(ProductTypes.Gum, productDoc.Content.ProductType);
                Assert.Equal("gum", candyDoc.Content.Name);

                gum = (Gum)productDoc.Content;

                Assert.Equal("product.candy.gum", gumDoc.Type);
                Assert.Equal(ProductTypes.Gum, gum.ProductType);
                Assert.Equal("gum", gum.Name);
                Assert.Equal(1, gum.Calories);
                Assert.Equal("spearmint", gum.Flavor);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Subentities()
        {
            using (var test = new TestDatabase())
            {
                var db = test.Database;

                //-------------------------------

                var catalogDoc = db.GetEntityDocument<Catalog>("catalog");

                catalogDoc.Content.TopSeller 
                    = new CandyBar()
                    {
                        Name = "BabyRuth",
                        Calories = 150,
                        HasNuts = true
                    };

                catalogDoc.Content.Products
                    = new Product[]
                    {
                        new CandyBar()
                        {
                            Name = "BabyRuth",
                            Calories = 150,
                            HasNuts = true
                        },
                        new Gum()
                        {
                            Name = "Juicyfruit",
                            Calories = 50,
                            Flavor = "Spearmint"
                        },
                        new Product()
                        {
                            Name = "Pepsi Cola"
                        }
                    };

                catalogDoc.Save();
                catalogDoc = db.GetEntityDocument<Catalog>("catalog");

                Assert.Equal("CATALOG", catalogDoc.Type);
                Assert.Equal("CATALOG", catalogDoc.Content.EntityType);

                Assert.Equal(ProductTypes.CandyBar, catalogDoc.Content.TopSeller.ProductType);
                Assert.Equal("BabyRuth", ((CandyBar)catalogDoc.Content.TopSeller).Name);
                Assert.Equal(150, ((CandyBar)catalogDoc.Content.TopSeller).Calories);
                Assert.True(((CandyBar)catalogDoc.Content.TopSeller).HasNuts);

                Assert.Equal(3, catalogDoc.Content.Products.Count);

                Assert.Equal(ProductTypes.CandyBar, catalogDoc.Content.Products[0].ProductType);
                Assert.Equal("BabyRuth", ((CandyBar)catalogDoc.Content.Products[0]).Name);
                Assert.Equal(150, ((CandyBar)catalogDoc.Content.Products[0]).Calories);
                Assert.True(((CandyBar)catalogDoc.Content.Products[0]).HasNuts);

                Assert.Equal(ProductTypes.Gum, catalogDoc.Content.Products[1].ProductType);
                Assert.Equal("Juicyfruit", ((Gum)catalogDoc.Content.Products[1]).Name);
                Assert.Equal(50, ((Gum)catalogDoc.Content.Products[1]).Calories);
                Assert.Equal("Spearmint", ((Gum)catalogDoc.Content.Products[1]).Flavor);

                Assert.Equal(ProductTypes.Product, catalogDoc.Content.Products[2].ProductType);
                Assert.Equal("Pepsi Cola", catalogDoc.Content.Products[2].Name);
            }
        }
            
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void InvalidCast()
        {
            // Verify the we get an exception if we attempt to load a document
            // whose entity does not derive from the document's entity type.

            using (var test = new TestDatabase())
            {
                var db = test.Database;

                //-------------------------------

                var candyDoc = db.GetEntityDocument<Candy>("candy");

                candyDoc.Content.Name = "candy";
                candyDoc.Content.Calories = 100;
                candyDoc.Save();
                candyDoc = db.GetEntityDocument<Candy>("candy");

                var gumDoc = db.GetEntityDocument<Gum>("gum");

                gumDoc.Content.Name = "gum";
                gumDoc.Content.Calories = 1;
                gumDoc.Content.Flavor = "spearmint";
                gumDoc.Save();
                gumDoc = db.GetEntityDocument<Gum>("gum");

                //-------------------------------
                // Attempt to read [Candy] and [Gum] as a [Computer] and
                // confirm that this fails because [Computer] does not
                // derive from [Candy].

                Assert.Throws<InvalidCastException>(() => db.GetEntityDocument<Computer>("candy"));
                Assert.Throws<InvalidCastException>(() => db.GetEntityDocument<Computer>("gum"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void UnknownTypes()
        {
            // Verify that we can we can read an entity with an unknown derived type.

            using (var test = new TestDatabase())
            {
                var db = test.Database;

                //-------------------------------
                // Create some documents

                var productDoc = db.GetEntityDocument<Product>("product");

                productDoc.Content.Name = "Computer";
                productDoc.Save();

                var candyDoc = db.GetEntityDocument<Candy>("candy");

                candyDoc.Content.Name = "candy";
                candyDoc.Content.Calories = 100;
                candyDoc.Save();
                candyDoc = db.GetEntityDocument<Candy>("candy");

                var gumDoc = db.GetEntityDocument<Gum>("gum");

                gumDoc.Content.Name = "gum";
                gumDoc.Content.Calories = 1;
                gumDoc.Content.Flavor = "spearmint";
                gumDoc.Save();
                gumDoc = db.GetEntityDocument<Gum>("gum");

                //-------------------------------
                // We're going to munge the [gum] document entity's type path
                // to simulate an unknown candy type.  Changing:
                //
                //      FROM:
                //      [product.candy.gum:product.candy:product]
                //
                //      TO:
                //      [product.candy.unknown:product.candy:product]
                //

                // $hack(jeff.lill):
                //
                // This makes assumptions about how entity type paths are formatted.

                var doc      = db.GetDocument("gum");
                var unsaved  = doc.CreateRevision();
                var content  = (JObject)unsaved.Properties[NeonPropertyNames.Content];
                var typePath = (string)content[DynamicEntity.EntityTypePathName];

                typePath = typePath.Replace("product.candy.gum:", "product.candy.unknown:");

                content                                       = (JObject)content.DeepClone();
                content[DynamicEntity.EntityTypePathName]    = typePath;
                unsaved.Properties[NeonPropertyNames.Content] = content;

                unsaved.Save();

                //-------------------------------
                // Read [Unknown] as [Product]

                productDoc = db.GetEntityDocument<Product>("gum");

                Assert.Equal(ProductTypes.Gum, productDoc.Content.ProductType);
                Assert.Equal("gum", productDoc.Content.Name);

                var candy = (Candy)productDoc.Content;

                Assert.Equal(ProductTypes.Gum, candy.ProductType);
                Assert.Equal("gum", candy.Name);
                Assert.Equal(1, candy.Calories);

                //-------------------------------
                // Read [Unknown] as [Candy]

                candyDoc = db.GetEntityDocument<Candy>("gum");

                Assert.Equal(ProductTypes.Gum, productDoc.Content.ProductType);
                Assert.Equal("gum", candyDoc.Content.Name);
                Assert.Equal(1, candyDoc.Content.Calories);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public void Equality()
        {
            var product1 = new Product();
            var product2 = new Product();

            Assert.True(product1 == product2);
            Assert.False(product1 != product2);

            product1.Name = "Foo";
            product2.Name = "Foo";
            Assert.True(product1 == product2);
            Assert.False(product1 != product2);

            product1.Name = "Foo";
            product2.Name = "Bar";
            Assert.False(product1 == product2);
            Assert.True(product1 != product2);

            var candy1 = new Candy();
            var candy2 = new Candy();

            Assert.True(candy1 == candy2);
            Assert.False(candy1 != candy2);

            candy1.Name = "Foo";
            candy2.Name = "Foo";
            candy1.Calories = 100;
            candy2.Calories = 100;
            Assert.True(candy1 == candy2);
            Assert.False(candy1 != candy2);

            candy1.Name = "Foo";
            candy2.Name = "Bar";
            candy1.Calories = 100;
            candy2.Calories = 100;
            Assert.False(candy1 == candy2);
            Assert.True(candy1 != candy2);

            candy1.Name = "Foo";
            candy2.Name = "Foo";
            candy1.Calories = 100;
            candy2.Calories = 200;
            Assert.False(candy1 == candy2);
            Assert.True(candy1 != candy2);
        }
    }
}
