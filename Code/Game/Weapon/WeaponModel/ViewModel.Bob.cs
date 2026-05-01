public sealed partial class ViewModel
{
	private const float HL2BobCycleMax = 0.45f;
	private const float HL2BobUp = 0.5f;
	private static float MaxViewModelLag = 1.5f;

	[ConVar( "sv_viewmodel_lag_do_angles", ConVarFlags.Cheat | ConVarFlags.Replicated )]
	private static bool ViewModelLagDoAngles { get; set; } = true;

	private float bobtime;
	private float lastbobtime;
	private float lateralBob;
	private float verticalBob;
	private Vector3 lastFacing;

	private void CalcViewModelView( CameraComponent camera )
	{
		var origin = camera.WorldPosition;
		var angles = camera.WorldRotation.Angles();
		var originalAngles = angles;

		var playerController = GetComponentInParent<PlayerController>();
		if ( GamePreferences.ViewBobbing )
		{
			AddViewmodelBob( playerController, ref origin, ref angles );
		}

		CalcViewModelLag( ref origin, ref angles, originalAngles );

		WorldPosition = origin;
		WorldRotation = angles.ToRotation();
	}

	private float CalcViewmodelBob( PlayerController playerController )
	{
		if ( Time.Delta == 0.0f || !playerController.IsValid() )
			return 0.0f;

		var speed = playerController.Velocity.WithZ( 0 ).Length;
		speed = speed.Clamp( -320.0f, 320.0f );

		var bobOffset = speed.Remap( 0.0f, 320.0f, 0.0f, 1.0f );

		bobtime += (Time.Now - lastbobtime) * bobOffset;
		lastbobtime = Time.Now;

		var cycle = bobtime - (int)(bobtime / HL2BobCycleMax) * HL2BobCycleMax;
		cycle /= HL2BobCycleMax;

		if ( cycle < HL2BobUp )
		{
			cycle = MathF.PI * cycle / HL2BobUp;
		}
		else
		{
			cycle = MathF.PI + MathF.PI * (cycle - HL2BobUp) / (1.0f - HL2BobUp);
		}

		verticalBob = speed * 0.005f;
		verticalBob = verticalBob * 0.3f + verticalBob * 0.7f * MathF.Sin( cycle );
		verticalBob = verticalBob.Clamp( -7.0f, 4.0f );

		cycle = bobtime - (int)(bobtime / HL2BobCycleMax * 2.0f) * HL2BobCycleMax * 2.0f;
		cycle /= HL2BobCycleMax * 2.0f;

		if ( cycle < HL2BobUp )
		{
			cycle = MathF.PI * cycle / HL2BobUp;
		}
		else
		{
			cycle = MathF.PI + MathF.PI * (cycle - HL2BobUp) / (1.0f - HL2BobUp);
		}

		lateralBob = speed * 0.005f;
		lateralBob = lateralBob * 0.3f + lateralBob * 0.7f * MathF.Sin( cycle );
		lateralBob = lateralBob.Clamp( -7.0f, 4.0f );

		return 0.0f;
	}

	private void AddViewmodelBob( PlayerController playerController, ref Vector3 origin, ref Angles angles )
	{
		var rotation = angles.ToRotation();

		CalcViewmodelBob( playerController );

		origin += rotation.Forward * verticalBob * 0.1f;
		origin += Vector3.Up * verticalBob * 0.1f;

		angles.roll += verticalBob * 0.5f;
		angles.pitch -= verticalBob * 0.4f;
		angles.yaw -= lateralBob * 0.3f;

		origin += rotation.Right * lateralBob * 0.8f;
	}

	private void CalcViewModelLag( ref Vector3 origin, ref Angles angles, Angles originalAngles )
	{
		var originalOrigin = origin;
		var viewModelAngles = angles;
		var forward = angles.ToRotation().Forward;

		if ( Time.Delta != 0.0f )
		{
			var difference = forward - lastFacing;
			var speed = 5.0f;
			var differenceLength = difference.Length;

			if ( differenceLength > MaxViewModelLag && MaxViewModelLag > 0.0f )
			{
				var scale = differenceLength / MaxViewModelLag;
				speed *= scale;
			}

			lastFacing += difference * speed * Time.Delta;
			lastFacing = lastFacing.Normal;
			origin -= difference * 5.0f;
		}

		if ( !ViewModelLagDoAngles )
			return;

		var originalRotation = originalAngles.ToRotation();
		forward = originalRotation.Forward;
		var right = originalRotation.Right;
		var up = originalRotation.Up;

		var pitch = originalAngles.pitch;
		if ( pitch > 180.0f )
		{
			pitch -= 360.0f;
		}
		else if ( pitch < -180.0f )
		{
			pitch += 360.0f;
		}

		if ( MaxViewModelLag == 0.0f )
		{
			origin = originalOrigin;
			angles = viewModelAngles;
		}

		origin += forward * -pitch * 0.035f;
		origin += right * -pitch * 0.03f;
		origin += up * -pitch * 0.02f;
	}
}