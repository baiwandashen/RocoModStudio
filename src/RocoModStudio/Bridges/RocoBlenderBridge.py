import argparse
import os
import sys
import bpy


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=['prepare', 'export'])
    parser.add_argument('--input', default='')
    parser.add_argument('--blend', required=True)
    parser.add_argument('--output-dir', required=True)
    parser.add_argument('--name', default='RocoAsset')
    parser.add_argument('--format', default='fbx', choices=['fbx', 'gltf', 'glb'])
    return parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_model(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == '.fbx':
        bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=True)
    elif ext in ('.gltf', '.glb'):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == '.obj':
        try:
            bpy.ops.wm.obj_import(filepath=path)
        except Exception:
            bpy.ops.import_scene.obj(filepath=path)
    else:
        raise RuntimeError(f'Unsupported model format: {ext}')


def export_model(path, fmt):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if fmt == 'fbx':
        bpy.ops.export_scene.fbx(filepath=path, use_selection=False, apply_scale_options='FBX_SCALE_ALL', bake_anim=True)
    elif fmt in ('gltf', 'glb'):
        bpy.ops.export_scene.gltf(filepath=path, export_format='GLB' if fmt == 'glb' else 'GLTF_SEPARATE', use_selection=False, export_animations=True)
    else:
        raise RuntimeError(f'Unsupported export format: {fmt}')


def main():
    args = parse_args()
    if args.mode == 'prepare':
        if not os.path.isfile(args.input):
            raise FileNotFoundError(args.input)
        clear_scene()
        import_model(args.input)
        os.makedirs(os.path.dirname(args.blend), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=args.blend)
        output = os.path.join(args.output_dir, args.name + '.' + args.format)
        export_model(output, args.format)
        print(f'BLENDER_BLEND={args.blend}')
        print(f'BLENDER_MODEL={output}')
    else:
        if not os.path.isfile(args.blend):
            raise FileNotFoundError(args.blend)
        bpy.ops.wm.open_mainfile(filepath=args.blend)
        output = os.path.join(args.output_dir, args.name + '.' + args.format)
        export_model(output, args.format)
        print(f'BLENDER_MODEL={output}')


if __name__ == '__main__':
    main()
