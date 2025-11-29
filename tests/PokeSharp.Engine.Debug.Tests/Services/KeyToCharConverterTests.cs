using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.Debug.Systems.Services;

namespace PokeSharp.Engine.Debug.Tests.Services;

public class KeyToCharConverterTests
{
    [Theory]
    [InlineData(Keys.A, false, 'a')]
    [InlineData(Keys.A, true, 'A')]
    [InlineData(Keys.Z, false, 'z')]
    [InlineData(Keys.Z, true, 'Z')]
    [InlineData(Keys.M, false, 'm')]
    [InlineData(Keys.M, true, 'M')]
    public void ToChar_WithLetters_ReturnsCorrectCharacter(Keys key, bool shift, char expected)
    {
        // Act
        var result = KeyToCharConverter.ToChar(key, shift);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Keys.D0, false, '0')]
    [InlineData(Keys.D0, true, ')')]
    [InlineData(Keys.D1, false, '1')]
    [InlineData(Keys.D1, true, '!')]
    [InlineData(Keys.D2, false, '2')]
    [InlineData(Keys.D2, true, '@')]
    [InlineData(Keys.D3, false, '3')]
    [InlineData(Keys.D3, true, '#')]
    [InlineData(Keys.D9, false, '9')]
    [InlineData(Keys.D9, true, '(')]
    public void ToChar_WithNumbers_ReturnsCorrectCharacter(Keys key, bool shift, char expected)
    {
        // Act
        var result = KeyToCharConverter.ToChar(key, shift);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Keys.Space, false, ' ')]
    [InlineData(Keys.Space, true, ' ')]
    [InlineData(Keys.OemPeriod, false, '.')]
    [InlineData(Keys.OemPeriod, true, '>')]
    [InlineData(Keys.OemComma, false, ',')]
    [InlineData(Keys.OemComma, true, '<')]
    [InlineData(Keys.OemQuestion, false, '/')]
    [InlineData(Keys.OemQuestion, true, '?')]
    [InlineData(Keys.OemSemicolon, false, ';')]
    [InlineData(Keys.OemSemicolon, true, ':')]
    public void ToChar_WithPunctuation_ReturnsCorrectCharacter(Keys key, bool shift, char expected)
    {
        // Act
        var result = KeyToCharConverter.ToChar(key, shift);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Keys.NumPad0, '0')]
    [InlineData(Keys.NumPad5, '5')]
    [InlineData(Keys.NumPad9, '9')]
    [InlineData(Keys.Add, '+')]
    [InlineData(Keys.Subtract, '-')]
    [InlineData(Keys.Multiply, '*')]
    [InlineData(Keys.Divide, '/')]
    [InlineData(Keys.Decimal, '.')]
    public void ToChar_WithNumpadKeys_ReturnsCorrectCharacter(Keys key, char expected)
    {
        // Act
        var result = KeyToCharConverter.ToChar(key, false);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.Back)]
    [InlineData(Keys.Delete)]
    [InlineData(Keys.Left)]
    [InlineData(Keys.Right)]
    [InlineData(Keys.Up)]
    [InlineData(Keys.Down)]
    [InlineData(Keys.Home)]
    [InlineData(Keys.End)]
    [InlineData(Keys.Tab)]
    [InlineData(Keys.Escape)]
    public void ToChar_WithNonPrintableKeys_ReturnsNull(Keys key)
    {
        // Act
        var result = KeyToCharConverter.ToChar(key, false);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToChar_WithOemOpenBrackets_ReturnsCorrectCharacters()
    {
        // Act
        var unshifted = KeyToCharConverter.ToChar(Keys.OemOpenBrackets, false);
        var shifted = KeyToCharConverter.ToChar(Keys.OemOpenBrackets, true);

        // Assert
        Assert.Equal('[', unshifted);
        Assert.Equal('{', shifted);
    }

    [Fact]
    public void ToChar_WithOemCloseBrackets_ReturnsCorrectCharacters()
    {
        // Act
        var unshifted = KeyToCharConverter.ToChar(Keys.OemCloseBrackets, false);
        var shifted = KeyToCharConverter.ToChar(Keys.OemCloseBrackets, true);

        // Assert
        Assert.Equal(']', unshifted);
        Assert.Equal('}', shifted);
    }
}
