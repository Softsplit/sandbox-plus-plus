using Sandbox.Rendering;

public abstract partial class ToolMode : Component
{
	public Toolgun Toolgun => GetComponent<Toolgun>();
	public Player Player => GetComponentInParent<Player>();

	/// <summary>
	/// The mode should set this true or false in OnControl to indicate if the current state is valid for performing actions.
	/// </summary>
	public bool IsValidState { get; protected set; } = true;

	public virtual void OnControl() { }

	public virtual void DrawScreen( Rect rect, HudPainter paint )
	{
		var t = $"{TypeLibrary.GetType( GetType() ).Icon} {GetType().Name}";

		var text = new TextRendering.Scope( t, Color.White, 64 );
		text.LineHeight = 0.75f;
		text.FontName = "Poppins";
		text.TextColor = Color.Orange;
		text.FontWeight = 700;

		paint.DrawText( text, rect, TextFlag.Center );

	}

	public virtual void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		painter.SetBlendMode( BlendMode.Normal );
		painter.DrawCircle( crosshair, 8, Color.Black.WithAlpha( 0.5f ) );
		painter.DrawCircle( crosshair, 4, Color.White );
	}
}
