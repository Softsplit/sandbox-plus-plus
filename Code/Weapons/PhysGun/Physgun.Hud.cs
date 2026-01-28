using Sandbox.Rendering;

public partial class Physgun : BaseCarryable
{
	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		painter.SetBlendMode( BlendMode.Normal );
		painter.DrawCircle( crosshair, 8, Color.Black.WithAlpha( 0.5f ) );
		painter.DrawCircle( crosshair, 4, Color.White );
	}
}
