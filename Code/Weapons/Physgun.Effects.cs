using Sandbox.Utility;

public partial class PhysGun
{
	[Property] public LineRenderer BeamRenderer { get; set; }
	[Property] public LineRendererLight BeamLight { get; set; }

	void UpdateBeam( Transform source, Vector3 end, Vector3 endNormal )
	{
		if ( !BeamRenderer.IsValid() || !BeamLight.IsValid() ) return;

		// obj
		if ( _state.GameObject.IsValid() )
		{
			BeamHighlight.Enabled = true;
			BeamHighlight.OverrideTargets = true;
			BeamHighlight.Targets.Clear();
			BeamHighlight.Targets.AddRange( _state.GameObject.GetComponents<Renderer>() );
			BeamHighlight.Width = 0.1f + Noise.Fbm( 3, Time.Now * 100.0f ) * 0.1f;
			BeamHighlight.Color = Color.Lerp( Color.Cyan, Color.White, Noise.Fbm( 3, Time.Now * 40.0f ) * 0.5f ) * 200.0f;
		}

		bool justEnabled = !BeamRenderer.Enabled;

		if ( BeamRenderer.VectorPoints.Count != 4 )
			BeamRenderer.VectorPoints = new List<Vector3>( [0, 0, 0, 0] );

		var distance = source.Position.Distance( end );

		BeamRenderer.VectorPoints[0] = source.Position;

		var targetMiddle = source.Position + source.Forward * distance * 0.33f;

		BeamRenderer.VectorPoints[1] = targetMiddle;
		BeamRenderer.VectorPoints[2] = Vector3.Lerp( (end + endNormal * 10), BeamRenderer.VectorPoints[1], 0.3f + MathF.Sin( Time.Now * 10.0f ) * 0.2f );
		BeamRenderer.VectorPoints[3] = end;

		if ( justEnabled )
		{
			BeamRenderer.Enabled = true;
			BeamLight.Enabled = true;
			BeamRenderer.VectorPoints[1] = targetMiddle;
		}
	}

	void CloseBeam()
	{
		BeamHighlight.Enabled = false;

		if ( !BeamRenderer.IsValid() || !BeamLight.IsValid() ) return;

		BeamRenderer.Enabled = false;
		BeamLight.Enabled = false;
	}
}
