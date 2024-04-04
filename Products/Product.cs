using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products;

public class Product
{
    public string BarCode { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }

    public bool IsProductValid()
    {
        return !string.IsNullOrEmpty(BarCode) && !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Description) && Price > 0;
    }
}
