using Sandbox.Rendering;
using Sandbox.Utility;

public class CameraWeapon : BaseWeapon
{
	float fov;
	float roll = 0;
	bool wasAttacking;

	[Property] SoundEvent CameraShoot { get; set; }

	public override bool WantsHideHud => true;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( IsProxy )
			return;

		fov = Screen.CreateVerticalFieldOfView( Preferences.FieldOfView, 9.0f / 16.0f );
		wasAttacking = false;
	}

	/// <summary>
	/// We want to control the camera fov
	/// </summary>
	public override void OnCameraSetup( Player player, Sandbox.CameraComponent camera )
	{
		if ( !player.Network.IsOwner || !Network.IsOwner ) return;

		camera.FieldOfView = fov;
		camera.WorldRotation = camera.WorldRotation * new Angles( 0, 0, roll );
	}

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		// We're zooming
		if ( Input.Down( "attack2" ) )
		{
			angles = default;
		}

		float sensitivity = fov.Remap( 1, 70, 0.01f, 1 );
		angles *= sensitivity;
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Input.Pressed( "reload" ) )
		{
			fov = Screen.CreateVerticalFieldOfView( Preferences.FieldOfView, 9.0f / 16.0f );
			roll = 0;
		}

		if ( Input.Down( "attack2" ) )
		{
			fov += Input.AnalogLook.pitch;
			fov = fov.Clamp( 1, 150 );
			roll -= Input.AnalogLook.yaw;
		}

		if ( Input.Down( "attack1" ) )
		{
			wasAttacking = true;
		}

		if ( wasAttacking && Input.Released( "attack1" ) )
		{
			Game.TakeScreenshot();
			Sandbox.Services.Stats.Increment( "photos", 1 );

			GameObject?.PlaySound( CameraShoot );
			wasAttacking = false;
		}
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		// nothing!
	}
}
