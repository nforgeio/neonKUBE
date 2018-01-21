//-----------------------------------------------------------------------------
// FILE:	    IInheritTest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
using Neon.DynamicData;

namespace Test.Neon.Models
{
    [DynamicInclude]
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

    [DynamicEntity(Type = ProductTypes.Product)]
    public interface IProduct
    {
        string Name { get; set; }

        [DynamicEntityProperty(IsTypeProperty = true)]
        ProductTypes ProductType { get; }
    }

    [DynamicEntity(Type = ProductTypes.Candy)]
    public interface ICandy : IProduct
    {
        int Calories { get; set; }
    }

    [DynamicEntity(Type = ProductTypes.Gum)]
    public interface IGum : ICandy
    {
        string Flavor { get; set; } 
    }

    [DynamicEntity(Type = ProductTypes.CandyBar)]
    public interface ICandyBar : ICandy
    {
        bool HasNuts { get; set; }
    }

    [DynamicEntity(Type = ProductTypes.Computer)]
    public interface IComputer : IProduct
    {
        bool IsLinux { get; set; }
    }

    [DynamicEntity(Type = "CATALOG")]
    public interface ICatalog
    {
        IProduct TopSeller { get; set; }
        IProduct[] Products { get; set; }

        [DynamicEntityProperty(IsTypeProperty = true)]
        string EntityType { get; }
    }
}
