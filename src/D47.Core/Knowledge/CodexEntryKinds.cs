using System.Collections.Frozen;

namespace D47.Core.Knowledge;

/// <summary>
/// The kind tree node each star and planet EntryID of the old <c>CodexBodies.tsv</c> (at <c>d989b1ff</c>) covered: a
/// star's kind, and a planet's or gas giant's group. <c>tools/codex-pictures-rekey.py</c> reads this table.
/// </summary>
public static class CodexEntryKinds
{
    public static readonly FrozenDictionary<long, string> Map = new Dictionary<long, string>
    {
        [1100101] = "star.o_blue_white_star.dwarf",                                    // O Type Star
        [1100201] = "star.b_blue_white_star.dwarf",                                    // B Type Star
        [1100202] = "star.b_blue_white_star.giant",                                    // B Type Giant
        [1100301] = "star.a_blue_white_star.dwarf",                                    // A Type Star
        [1100302] = "star.a_blue_white_star.giant",                                    // A Type Giant
        [1100303] = "star.a_blue_white_super_giant_star.supergiant",                   // A Type Supergiant
        [1100401] = "star.f_white_star.dwarf",                                         // F Type Star
        [1100402] = "star.f_white_star.giant",                                         // F Type Giant
        [1100501] = "star.g_white_yellow_star.dwarf",                                  // G Type Star
        [1100502] = "star.g_white_yellow_star.giant",                                  // G Type Giant
        [1100601] = "star.k_yellow_orange_star.dwarf",                                 // K Type Star
        [1100602] = "star.k_yellow_orange_giant_star.giant",                           // K Type Giant
        [1100701] = "star.m_red_dwarf_star.dwarf",                                     // M Type Star
        [1100801] = "star.l_brown_dwarf_star.dwarf",                                   // L Type Star
        [1100901] = "star.t_brown_dwarf_star.dwarf",                                   // T Type Star
        [1101001] = "star.t_tauri_star.dwarf",                                         // T Tauri Star
        [1101101] = "star.herbig_ae_be_star.dwarf",                                    // AeBe Type Star
        [1101201] = "star.y_brown_dwarf_star.dwarf",                                   // Y Type Star
        [1101404] = "star.c_star.hypergiant",                                          // C Type Hypergiant
        [1102002] = "star.s_type_star.giant",                                          // S Type Giant
        [1102101] = "star.wolf_rayet_star.supergiant",                                 // W Type Star
        [1102201] = "star.white_dwarf_d_star",                                         // D Type Star
        [1102202] = "star.white_dwarf_da_star",                                        // DA Type Star
        [1102203] = "star.white_dwarf_dab_star",                                       // DAB Type Star
        [1102206] = "star.white_dwarf_daz_star",                                       // DAZ Type Star
        [1102212] = "star.white_dwarf_dq_star",                                        // DQ Type Star
        [1102213] = "star.white_dwarf_dc_star",                                        // DC Type Star
        [1102300] = "star.neutron_star",                                               // Neutron Star
        [1102400] = "star.black_hole",                                                 // Black Hole
        [1200101] = "gas.water_giant",                                                 // Water Giant
        [1200301] = "gas.gas_giant_with_water_based_life",                             // Gas Giant with Water-Based Life
        [1200401] = "gas.gas_giant_with_ammonia_based_life",                           // Gas Giant with Ammonia-Based Life
        [1200501] = "gas.class_i_gas_giant",                                           // Class I Gas Giant
        [1200601] = "gas.class_ii_gas_giant",                                          // Class II Gas Giant
        [1200701] = "gas.class_iii_gas_giant",                                         // Class III Gas Giant
        [1200801] = "gas.class_iv_gas_giant",                                          // Class IV Gas Giant
        [1200901] = "gas.class_v_gas_giant",                                           // Class V Gas Giant
        [1201001] = "gas.helium_rich_gas_giant",                                       // Helium-Rich Gas Giant
        [1300100] = "planet.earth_like_world",                                         // Earth-Like Planet
        [1300202] = "planet.ammonia_world.not_terraformable",                          // Ammonia World
        [1300301] = "planet.water_world.not_terraformable",                            // Water World
        [1300302] = "planet.water_world.terraformable",                                // Water World, Terraformable
        [1300401] = "planet.metal_rich_body.not_terraformable.no_atmosphere",          // Metal-Rich Body
        [1300501] = "planet.high_metal_content_world.not_terraformable.no_atmosphere", // High Metal Content Body
        [1300502] = "planet.high_metal_content_world.terraformable.no_atmosphere",     // High Metal Content Body, Terraformable
        [1300601] = "planet.rocky_body.not_terraformable.no_atmosphere",               // Rocky Body
        [1300602] = "planet.rocky_body.terraformable.no_atmosphere",                   // Rocky Body, Terraformable
        [1300701] = "planet.rocky_ice_world.not_terraformable.no_atmosphere",          // Rocky Ice Body
        [1300801] = "planet.icy_body.not_terraformable.no_atmosphere",                 // Icy Body
        [1301401] = "planet.metal_rich_body.not_terraformable.atmosphere",             // Metal-Rich Body with Atmosphere
        [1301501] = "planet.high_metal_content_world.not_terraformable.atmosphere",    // High Metal Content Body with Atmosphere
        [1301502] = "planet.high_metal_content_world.terraformable.atmosphere",        // High Metal Content Body with Atmosphere, Terraformable
        [1301601] = "planet.rocky_body.not_terraformable.atmosphere",                  // Rocky Body with Atmosphere
        [1301602] = "planet.rocky_body.terraformable.atmosphere",                      // Rocky Body with Atmosphere, Terraformable
        [1301701] = "planet.rocky_ice_world.not_terraformable.atmosphere",             // Rocky Ice Body with Atmosphere
        [1301801] = "planet.icy_body.not_terraformable.atmosphere",                    // Icy Body with Atmosphere
    }.ToFrozenDictionary();

    /// <summary>The kind key an old star or planet EntryID covered; null for any other EntryID.</summary>
    public static string? Of(long entryId) => Map.GetValueOrDefault(entryId);
}
