using System.IO;
using System.Linq;
using System.Collections.Generic;
using Hunspell;
using Xunit;

namespace Hunspell.Tests;

/// <summary>
/// Tests for morphological analysis and stemming functionality.
/// </summary>
public class MorphologicalAnalysisTests
{
    private static string D(string baseName, string ext)
    {
        var parts = baseName.Split('/');
        var fileName = parts[^1];
        return Path.Combine("..", "..", "..", "dictionaries", baseName, fileName + ext);
    }

    [Fact]
    public void Stem_SimpleWord_ReturnsStem()
    {
        // Arrange
        var aff = D("morph", ".aff");
        var dic = D("morph", ".dic");
        
        using var sp = new HunspellSpellChecker(aff, dic);
        
        // Act - "drinks" should stem to "drink"
        var stems = sp.Stem("drink");
        
        // Assert
        Assert.NotEmpty(stems);
        Assert.Contains("drink", stems);
    }

    [Fact]
    public void Stem_InflectedWord_ReturnsBaseStem()
    {
        // Arrange
        var aff = D("morph", ".aff");
        var dic = D("morph", ".dic");
        
        using var sp = new HunspellSpellChecker(aff, dic);
        
        // Act - "drinks" should stem to "drink"
        var stems = sp.Stem("drinks");
        
        // Assert
        Assert.NotEmpty(stems);
        Assert.Contains("drink", stems);
    }

    [Fact]
    public void Analyze_SimpleWord_ReturnsAnalysis()
    {
        // Arrange
        var aff = D("morph", ".aff");
        var dic = D("morph", ".dic");
        
        using var sp = new HunspellSpellChecker(aff, dic);
        
        // Act
        var analyses = sp.Analyze("drink");
        
        // Assert
        Assert.NotEmpty(analyses);
        // Should contain part of speech or other morphological data
    }

    [Fact]
    public void Analyze_InflectedWord_ReturnsAnalysisWithAffix()
    {
        // Arrange
        var aff = D("morph", ".aff");
        var dic = D("morph", ".dic");
        
        using var sp = new HunspellSpellChecker(aff, dic);
        
        // Act - "drinks" = "drink" + "s" suffix
        var analyses = sp.Analyze("drinks");
        
        // Assert
        Assert.NotEmpty(analyses);
        // Should contain morphological information about plural/3rd person
    }
}
