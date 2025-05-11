using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Data_row> Rows { get; set; }
    public DbSet<Item> Items { get; set; }
    // добавьте другие DbSet для ваших сущностей
}

public class Data_row
{
    public int layer { get; set; }
    public int archive_itemid { get; set; }
    public BigInteger source_time { get; set; }
    public BigInteger server_time { get; set; }
    public int status_code { get; set; }
    public float value { get; set; }
    public string s_value { get; set; }
}

public class Item
{
    public int id { get; set; }
    public int project_id { get; set; }
    public int itemid { get; set; }
    public VariantType path { get; set; }
    public VariantType name { get; set; }
    public BigInteger first_time { get; set; }
    public BigInteger last_time { get; set; }
    public BigInteger count { get; set; }
    public int type { get; set; }
}