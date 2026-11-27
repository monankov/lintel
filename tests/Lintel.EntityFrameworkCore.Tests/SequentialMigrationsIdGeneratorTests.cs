using Lintel.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.EntityFrameworkCore.Tests;

public class SequentialMigrationsIdGeneratorTests
{
    public sealed class NumberedContext(DbContextOptions<NumberedContext> options) : DbContext(options);

    public sealed class TimestampContext(DbContextOptions<TimestampContext> options) : DbContext(options);

    public sealed class EmptyContext(DbContextOptions<EmptyContext> options) : DbContext(options);

    public sealed class ElsewhereContext(DbContextOptions<ElsewhereContext> options) : DbContext(options);

    public sealed class FullContext(DbContextOptions<FullContext> options) : DbContext(options);

    [DbContext(typeof(FullContext))]
    [Migration("9999_Last")]
    public sealed class Last : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }
    }

    /// <summary>Configured in OnConfiguring, as EF Core tools create it: its options are those of DbContext.</summary>
    public sealed class ConfiguredContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:").UseSequentialMigrationIds();
    }

    [DbContext(typeof(ConfiguredContext))]
    [Migration("0002_Configured")]
    public sealed class ConfiguredSecond : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }
    }

    [DbContext(typeof(ElsewhereContext))]
    [Migration("0007_InTestAssembly")]
    public sealed class Seventh : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }
    }

    [DbContext(typeof(NumberedContext))]
    [Migration("0001_InitialCreate")]
    public sealed class First : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }
    }

    [DbContext(typeof(NumberedContext))]
    [Migration("0003_AddOrders")]
    public sealed class Third : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }
    }

    [DbContext(typeof(TimestampContext))]
    [Migration("20260920170115_InitialCreate")]
    public sealed class Timestamped : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }
    }

    private static IMigrationsIdGenerator Generator<TContext>(Action<DbContextOptionsBuilder<TContext>>? configure = null)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>().UseSqlite("Data Source=:memory:").UseSequentialMigrationIds();
        configure?.Invoke((DbContextOptionsBuilder<TContext>)builder);
        var context = (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
        return context.GetService<IMigrationsIdGenerator>();
    }

    [Fact]
    public void Next_number_follows_the_largest_migration_of_the_context()
    {
        var generator = Generator<NumberedContext>();

        generator.Should().BeOfType<SequentialMigrationsIdGenerator>();
        generator.GenerateId("AddCustomers").Should().Be("0004_AddCustomers");
    }

    [Fact]
    public void Context_configured_in_OnConfiguring_counts_its_own_migrations()
    {
        using var context = new ConfiguredContext();

        context.GetService<IMigrationsIdGenerator>().GenerateId("Next").Should().Be("0003_Next");
    }

    [Fact]
    public void First_migration_is_0001_and_other_contexts_are_not_counted()
        => Generator<EmptyContext>().GenerateId("InitialCreate").Should().Be("0001_InitialCreate");

    [Fact]
    public void Migrations_assembly_from_options_is_scanned()
    {
        var byName = Generator<EmptyContext>(o => o.UseSqlite("Data Source=:memory:", s => s.MigrationsAssembly(typeof(SequentialMigrationsIdGeneratorTests).Assembly.GetName().Name)));
        var byObject = Generator<EmptyContext>(o => o.UseSqlite("Data Source=:memory:", s => s.MigrationsAssembly(typeof(SequentialMigrationsIdGeneratorTests).Assembly)));
        var elsewhere = Generator<ElsewhereContext>(o => o.UseSqlite("Data Source=:memory:", s => s.MigrationsAssembly(typeof(DbContext).Assembly)));

        byName.GenerateId("X").Should().Be("0001_X");
        byObject.GenerateId("X").Should().Be("0001_X");
        elsewhere.GenerateId("X").Should().Be("0001_X", "its migration 0007 is in the test assembly, not in the configured one");
        Generator<ElsewhereContext>().GenerateId("X").Should().Be("0008_X", "the same context with the default assembly");
    }

    [Fact]
    public void Existing_timestamp_migrations_stop_generation()
    {
        var generate = () => Generator<TimestampContext>().GenerateId("Next");

        generate.Should().Throw<InvalidOperationException>().WithMessage("*'20260920170115_InitialCreate'*");
    }

    [Theory]
    [InlineData("0001_InitialCreate", true, "InitialCreate")]
    [InlineData("20260920170115_Init_Data", true, "Init_Data")]
    [InlineData("InitialCreate", false, "InitialCreate")]
    [InlineData("_Name", false, "Name")]
    public void Ids_are_recognized_and_names_extracted(string value, bool valid, string name)
    {
        var generator = Generator<EmptyContext>();

        generator.IsValidId(value).Should().Be(valid);
        generator.GetName(value).Should().Be(name);
    }

    [Fact]
    public void Numbers_run_out_after_9999()
    {
        var generate = () => Generator<FullContext>().GenerateId("More");

        generate.Should().Throw<InvalidOperationException>().WithMessage("*4 digits*");
    }

    [Fact]
    public void Extension_describes_itself_and_shares_the_service_provider()
    {
        var options = new DbContextOptionsBuilder<EmptyContext>().UseSqlite("Data Source=:memory:").UseSequentialMigrationIds().Options;
        var extension = options.FindExtension<SequentialMigrationsOptionsExtension>()!;
        var info = extension.Info;
        var debugInfo = new Dictionary<string, string>();

        info.PopulateDebugInfo(debugInfo);

        info.IsDatabaseProvider.Should().BeFalse();
        info.LogFragment.Should().Contain("SequentialMigrationIds");
        info.GetServiceProviderHashCode().Should().Be(new SequentialMigrationsOptionsExtension().Info.GetServiceProviderHashCode());
        info.ShouldUseSameServiceProvider(new SequentialMigrationsOptionsExtension().Info).Should().BeTrue();
        info.ShouldUseSameServiceProvider(options.FindExtension<CoreOptionsExtension>()!.Info).Should().BeFalse();
        debugInfo.Should().ContainKey("Lintel:SequentialMigrationIds");
        extension.Invoking(e => e.Validate(options)).Should().NotThrow();
        extension.Invoking(e => e.ApplyServices(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Arguments_are_checked()
    {
        var generator = Generator<EmptyContext>();
        var actions = new Action[]
        {
            () => generator.GenerateId(" "),
            () => generator.GetName(null!),
            () => generator.IsValidId(null!),
            () => _ = new SequentialMigrationsIdGenerator(null!, null!),
            () => ((DbContextOptionsBuilder)null!).UseSequentialMigrationIds(),
        };
        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentException>();
        }
    }
}
