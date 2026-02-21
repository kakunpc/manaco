using nadena.dev.ndmf;
using com.kakunvr.manaco;
using com.kakunvr.manaco.Editor;

// NDMFへのプラグイン登録。
// GeneratingフェーズはModularAvatarの処理後、最終ビルド前に実行されます。
[assembly: ExportsPlugin(typeof(CustomEyeShaderCorePlugin))]

namespace com.kakunvr.manaco
{
    public class CustomEyeShaderCorePlugin : Plugin<CustomEyeShaderCorePlugin>
    {
        public override string QualifiedName => "jp.kakunvr.custom-eye-shader-core";
        public override string DisplayName => "Custom Eye Shader Core";

        protected override void Configure()
        {
            // ModularAvatarのTransformingフェーズ後に実行するためGeneratingを使用
            InPhase(BuildPhase.Generating)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run("Apply Custom Eye SubMesh", ctx => new CustomEyeShaderCorePass().Execute(ctx))
                .PreviewingWith(new CustomEyeShaderCorePreviewFilter());
        }
    }
}
