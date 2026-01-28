using Sandbox.Rendering;

public static class HudPainterExtensions
{
	public static Color HudColor => new Color( 1f, 0.863f, 0f );

	public static void DrawHudElement( this HudPainter hud, string text, Vector2 position, Texture icon = null, float iconSize = 32f, TextFlag flags = TextFlag.LeftCenter )
	{
		var textScope = new TextRendering.Scope( text, Color.White, 32 * Hud.Scale );
		textScope.TextColor = "white";
		textScope.FontName = "Poppins";
		textScope.FontWeight = 600;
		textScope.Shadow = new TextRendering.Shadow { Enabled = true, Color = "#f506", Offset = 0, Size = 2 };

		hud.SetBlendMode( BlendMode.Lighten );

		if ( icon != null )
		{
			if ( flags.HasFlag( TextFlag.Right ) )
				position.x -= iconSize * Hud.Scale;

			hud.DrawTexture( icon, new Rect( position, iconSize * Hud.Scale ), textScope.TextColor );
		}

		const float padding = 16f;

		if ( flags.HasFlag( TextFlag.Left ) )
			position.x += (iconSize + padding) * Hud.Scale;

		var rect = new Rect( position, new Vector2( 256 * Hud.Scale, iconSize * Hud.Scale ) );
		if ( flags.HasFlag( TextFlag.Right ) )
			rect.Right = rect.Left - padding * Hud.Scale;

		hud.DrawText( textScope, rect, flags );
	}

	private static void DrawBackground( this HudPainter hud, Rect rect, float cornerRadius = 8f )
	{
		var bgColor = new Color( 0f, 0f, 0f, 0.2f );
		hud.SetBlendMode( BlendMode.Normal );
		hud.DrawRect( rect, bgColor, cornerRadius * Hud.Scale );
	}

	public static void DrawHealth( this HudPainter hud, int health, Vector2 position )
	{
		float scale = Hud.Scale;
		var hudColor = HudColor;

		float panelWidth = 220f * scale;
		float panelHeight = 80f * scale;

		var panelRect = new Rect( position.x, position.y - panelHeight, panelWidth, panelHeight );

		hud.DrawBackground( panelRect );

		hud.SetBlendMode( BlendMode.Lighten );

		var labelScope = new TextRendering.Scope( "HEALTH", hudColor, 18f * scale );
		labelScope.FontName = "Poppins";
		labelScope.FontWeight = 700;

		var labelRect = new Rect( panelRect.Left + 12f * scale, panelRect.Top + 6f * scale, 80f * scale, panelHeight );
		hud.DrawText( labelScope, labelRect, TextFlag.LeftCenter );

		var valueScope = new TextRendering.Scope( health.ToString(), hudColor, 56f * scale );
		valueScope.FontName = "Poppins";
		valueScope.FontWeight = 700;

		var valueRect = new Rect( panelRect.Left + 90f * scale, panelRect.Top, 106f * scale, panelHeight );
		hud.DrawText( valueScope, valueRect, TextFlag.RightCenter );
	}

	public static void DrawAmmo( this HudPainter hud, int clipAmmo, int reserveAmmo, Vector2 position, bool usesClips = true )
	{
		float scale = Hud.Scale;
		var hudColor = HudColor;

		float panelWidth = 260f * scale;
		float panelHeight = 80f * scale;

		var panelRect = new Rect( position.x - panelWidth, position.y - panelHeight, panelWidth, panelHeight );

		hud.DrawBackground( panelRect );

		hud.SetBlendMode( BlendMode.Lighten );

		var labelScope = new TextRendering.Scope( "AMMO", hudColor, 18f * scale );
		labelScope.FontName = "Poppins";
		labelScope.FontWeight = 700;

		var labelRect = new Rect( panelRect.Left + 12f * scale, panelRect.Top + 6f * scale, 60f * scale, panelHeight );
		hud.DrawText( labelScope, labelRect, TextFlag.LeftCenter );

		if ( usesClips )
		{
			var clipScope = new TextRendering.Scope( clipAmmo.ToString(), hudColor, 56f * scale );
			clipScope.FontName = "Poppins";
			clipScope.FontWeight = 700;

			var clipRect = new Rect( panelRect.Left + 90f * scale, panelRect.Top, 80f * scale, panelHeight );
			hud.DrawText( clipScope, clipRect, TextFlag.LeftCenter );

			var reserveScope = new TextRendering.Scope( reserveAmmo.ToString(), hudColor, 32f * scale );
			reserveScope.FontName = "Poppins";
			reserveScope.FontWeight = 600;

			var reserveRect = new Rect( panelRect.Left + 160f * scale, panelRect.Top + 9f * scale, 76f * scale, panelHeight );
			hud.DrawText( reserveScope, reserveRect, TextFlag.RightCenter );
		}
		else
		{
			var ammoScope = new TextRendering.Scope( reserveAmmo.ToString(), hudColor, 56f * scale );
			ammoScope.FontName = "Poppins";
			ammoScope.FontWeight = 700;

			var ammoRect = new Rect( panelRect.Left + 70f * scale, panelRect.Top, 166f * scale, panelHeight );
			hud.DrawText( ammoScope, ammoRect, TextFlag.RightCenter );
		}
	}

	public static void DrawHudElementWithLabel( this HudPainter hud, string label, string value, Vector2 position, TextFlag flags = TextFlag.LeftBottom, string secondaryValue = null )
	{
		hud.SetBlendMode( BlendMode.Lighten );

		var hudColor = HudColor;
		var shadowColor = new Color( 0, 0, 0, 0.5f );

		var labelScope = new TextRendering.Scope( label, hudColor, 14 * Hud.Scale );
		labelScope.FontName = "Poppins";
		labelScope.FontWeight = 700;
		labelScope.Shadow = new TextRendering.Shadow { Enabled = true, Color = shadowColor, Offset = 1, Size = 2 };

		var valueScope = new TextRendering.Scope( value, hudColor, 48 * Hud.Scale );
		valueScope.FontName = "Poppins";
		valueScope.FontWeight = 700;
		valueScope.Shadow = new TextRendering.Shadow { Enabled = true, Color = shadowColor, Offset = 1, Size = 3 };

		float labelHeight = 18 * Hud.Scale;
		float valueHeight = 52 * Hud.Scale;
		float totalHeight = labelHeight + valueHeight;

		Vector2 labelPos;
		Vector2 valuePos;

		if ( flags.HasFlag( TextFlag.Right ) )
		{
			labelPos = position - new Vector2( 0, totalHeight );
			valuePos = position - new Vector2( 0, valueHeight );

			var labelRect = new Rect( labelPos - new Vector2( 256 * Hud.Scale, 0 ), new Vector2( 256 * Hud.Scale, labelHeight ) );
			var valueRect = new Rect( valuePos - new Vector2( 256 * Hud.Scale, 0 ), new Vector2( 256 * Hud.Scale, valueHeight ) );

			hud.DrawText( labelScope, labelRect, TextFlag.RightTop );
			hud.DrawText( valueScope, valueRect, TextFlag.RightTop );

			if ( !string.IsNullOrEmpty( secondaryValue ) )
			{
				var secondaryScope = new TextRendering.Scope( secondaryValue, hudColor.WithAlpha( 0.7f ), 24 * Hud.Scale );
				secondaryScope.FontName = "Poppins";
				secondaryScope.FontWeight = 600;
				secondaryScope.Shadow = new TextRendering.Shadow { Enabled = true, Color = shadowColor, Offset = 1, Size = 2 };

				var secondaryRect = new Rect( valuePos - new Vector2( 256 * Hud.Scale, -valueHeight * 0.4f ), new Vector2( 256 * Hud.Scale, 28 * Hud.Scale ) );
				hud.DrawText( secondaryScope, secondaryRect, TextFlag.RightTop );
			}
		}
		else
		{
			labelPos = position - new Vector2( 0, totalHeight );
			valuePos = position - new Vector2( 0, valueHeight );

			var labelRect = new Rect( labelPos, new Vector2( 256 * Hud.Scale, labelHeight ) );
			var valueRect = new Rect( valuePos, new Vector2( 256 * Hud.Scale, valueHeight ) );

			hud.DrawText( labelScope, labelRect, TextFlag.LeftTop );
			hud.DrawText( valueScope, valueRect, TextFlag.LeftTop );
		}
	}
}
