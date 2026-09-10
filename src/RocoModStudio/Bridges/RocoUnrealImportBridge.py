import json
import os
import sys
import unreal


def load_config():
    if '--' not in sys.argv:
        raise RuntimeError('Missing bridge JSON path after --')
    path = sys.argv[sys.argv.index('--') + 1]
    with open(path, 'r', encoding='utf-8') as handle:
        return json.load(handle)


def main():
    config = load_config()
    source = config['source']
    destination_path = config['destinationPath'].rstrip('/')
    asset_name = config.get('assetName') or os.path.splitext(os.path.basename(source))[0]
    skeleton = config.get('skeletonPath', '')

    task = unreal.AssetImportTask()
    task.filename = source
    task.destination_path = destination_path
    task.destination_name = asset_name
    task.automated = True
    task.replace_existing = True
    task.save = True
    options = {'import_materials': True, 'import_textures': True, 'import_animations': True}
    if skeleton:
        options['skeleton'] = skeleton
    task.options = options
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])

    imported = [str(path) for path in task.imported_object_paths]
    if not imported:
        raise RuntimeError('Unreal import produced no assets')
    unreal.EditorAssetLibrary.save_directory(destination_path, only_if_is_dirty=True, recursive=True)
    print('UNREAL_IMPORTED=' + json.dumps(imported))


if __name__ == '__main__':
    main()
