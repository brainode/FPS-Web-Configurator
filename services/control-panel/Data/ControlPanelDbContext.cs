// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;
using Microsoft.EntityFrameworkCore;

namespace control_panel.Data;

public sealed class ControlPanelDbContext(DbContextOptions<ControlPanelDbContext> options) : DbContext(options)
{
    public DbSet<PanelUser> Users => Set<PanelUser>();
    public DbSet<GameConfiguration> GameConfigurations => Set<GameConfiguration>();
    public DbSet<PanelSetting> PanelSettings => Set<PanelSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PanelUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<GameConfiguration>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.GameKey).IsUnique();
            entity.Property(x => x.GameKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.JsonContent).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<PanelSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.SettingKey).IsUnique();
            entity.Property(x => x.SettingKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.JsonContent).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
        });
    }
}
