using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Data_row> DataRows { get; set; }
    public DbSet<Item> items { get; set; }
    public DbSet<ScadaString> ScadaStrings { get;set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Data_row>().HasNoKey(); // Отключение требования первичного ключа
    }
}
[Table("data_row", Schema = "public")] 
public class Data_row
{
    public int layer { get; set; }
    public int archive_itemid { get; set; }
    public long source_time { get; set; }
    public long server_time { get; set; }
    public int status_code { get; set; }
    [Column("value")]
    public float value { get; set; }
}


public class Item
{
    public int id { get; set; }
    public int project_id { get; set; }
    public long itemid { get; set; }
    public string? path { get; set; }
    public string? name { get; set; }
    public long first_time { get; set; }
    public long last_time { get; set; }
    public long count { get; set; }
    public int type { get; set; }
}
[Table("SCADAstrings", Schema = "public")] 
public class ScadaString
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("scada_string", TypeName = "text")]
    public string Value { get; set; } = null!;
}