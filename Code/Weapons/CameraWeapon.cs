using Sandbox.Rendering;

public sealed class CameraWeapon : BaseWeapon
{
	float fov;
	float roll = 0;

	bool focusing;

	[Property] SoundEvent CameraShoot { get; set; }

	/// <summary>
	/// The RT camera's resolution 
	/// </summary>
	private static int _cameraResolution = 512;

	/// <summary>
	/// The render target texture produced by this camera. Read by <see cref="TVEntity"/>.
	/// </summary>
	public Texture RenderTexture => _renderTexture;

	private Texture _renderTexture;
	private CameraComponent _rtCamera;

	public override bool WantsHideHud => true;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		EnsureRTCamera();
		EnsureRenderTexture();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		CleanupRenderTexture();
		_rtCamera = null;
	}

	protected override void OnDestroy()
	{
		CleanupRenderTexture();
		_rtCamera = null;
	}

	protected override void OnPreRender()
	{
		if ( !_rtCamera.IsValid() ) return;

		EnsureRenderTexture();

		if ( HasOwner && Scene.Camera.IsValid() )
		{
			// When held, mirror the player's camera so the TV shows their POV.
			// TODO: network some props to the TV so they show up in the RT camera when held by a player other than the host.
			_rtCamera.WorldPosition = Scene.Camera.WorldPosition;
			_rtCamera.WorldRotation = Scene.Camera.WorldRotation;
			_rtCamera.FieldOfView = Scene.Camera.FieldOfView;

			if ( !_rtCamera.RenderExcludeTags.Has( "viewer" ) )
				_rtCamera.RenderExcludeTags.Add( "viewer" );
		}
		else
		{
			_rtCamera.RenderExcludeTags.Remove( "viewer" );
			_rtCamera.FieldOfView = 40f;
		}
	}

	/// <summary>
	/// We want to control the camera fov when held by a player.
	/// </summary>
	public override void OnCameraSetup( Player player, Sandbox.CameraComponent camera )
	{
		if ( !player.Network.IsOwner || !Network.IsOwner ) return;

		if ( fov > 0 )
			camera.FieldOfView = fov;

		camera.WorldRotation = camera.WorldRotation * new Angles( 0, 0, roll );
	}

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		if ( Input.Down( "attack2" ) )
		{
			angles = default;
		}

		var currentFov = fov > 0 ? fov : Scene.Camera.FieldOfView;
		float sensitivity = currentFov.Remap( 1, 70, 0.01f, 1 );
		angles *= sensitivity;
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Input.Pressed( "reload" ) )
		{
			fov = 0;
			roll = 0;
		}

		if ( Input.Down( "attack2" ) )
		{
			fov = ((fov > 0 ? fov : Scene.Camera.FieldOfView) + Input.AnalogLook.pitch).Clamp( 1, 150 );
			roll -= Input.AnalogLook.yaw;
		}

		if ( focusing && Input.Released( "attack1" ) )
		{
			Game.TakeScreenshot();
			Sandbox.Services.Stats.Increment( "photos", 1 );

			GameObject?.PlaySound( CameraShoot );
		}

		focusing = Input.Down( "attack1" );
	}

	private void EnsureRTCamera()
	{
		_rtCamera = GetComponentInChildren<CameraComponent>( true );

		if ( _rtCamera is null )
		{
			var go = new GameObject( GameObject, true, "rt_camera" );
			_rtCamera = go.AddComponent<CameraComponent>();
		}

		_rtCamera.IsMainCamera = false;
		_rtCamera.BackgroundColor = Color.Black;
		_rtCamera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;
		_rtCamera.FieldOfView = Scene.Camera.FieldOfView;
		_rtCamera.RenderExcludeTags.Add( "viewmodel" );
	}

	private void EnsureRenderTexture()
	{
		if ( _renderTexture.IsValid() && _renderTexture.Width == _cameraResolution && _renderTexture.Height == _cameraResolution )
			return;

		CleanupRenderTexture();

		_renderTexture = Texture.CreateRenderTarget()
			.WithSize( _cameraResolution, _cameraResolution )
			.Create();

		if ( _rtCamera.IsValid() )
		{
			_rtCamera.RenderTarget = _renderTexture;
		}
	}

	private void CleanupRenderTexture()
	{
		if ( _rtCamera.IsValid() )
		{
			_rtCamera.RenderTarget = null;
		}

		_renderTexture?.Dispose();
		_renderTexture = null;
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		// nothing!
	}
}
