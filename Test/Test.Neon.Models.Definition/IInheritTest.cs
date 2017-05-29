//-----------------------------------------------------------------------------
// FILE:	    IInheritTest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

#pragma warning disable 1591

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;

namespace Test.Neon.Models
{
    [Include]
    public enum ProductTypes
    {
        [EnumMember(Value = "product")]
        Product,

        [EnumMember(Value = "product.candy")]
        Candy,

        [EnumMember(Value = "product.candy.gum")]
        Gum,

        [EnumMember(Value = "product.candy.bar")]
        CandyBar,

        [EnumMember(Value = "product.computer")]
        Computer,

        [EnumMember(Value = "catalog")]
        Catalog
    }

    [Entity(Type = ProductTypes.Product)]
    public interface IProduct
    {
        string Name { get; set; }

        [EntityProperty(IsTypeProperty = true)]
        ProductTypes ProductType { get; }
    }

    [Entity(Type = ProductTypes.Candy)]
    public interface ICandy : IProduct
    {
        int Calories { get; set; }
    }

    [Entity(Type = ProductTypes.Gum)]
    public interface IGum : ICandy
    {
        string Flavor { get; set; } 
    }

    [Entity(Type = ProductTypes.CandyBar)]
    public interface ICandyBar : ICandy
    {
        bool HasNuts { get; set; }
    }

    [Entity(Type = ProductTypes.Computer)]
    public interface IComputer : IProduct
    {
        bool IsLinux { get; set; }
    }

    [Entity(Type = "CATALOG")]
    public interface ICatalog
    {
        IProduct TopSeller { get; set; }
        IProduct[] Products { get; set; }

        [EntityProperty(IsTypeProperty = true)]
        string EntityType { get; }
    }
}
