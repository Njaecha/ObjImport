import bpy

# Function to determine if the image is likely a diffuse map based on naming convention
def is_diffuse_image(image_name):
    # Define a list of keywords that can identify a diffuse texture
    diffuse_keywords = ['diffuse', 'basecolor', 'color', '_d.png', '_d.tex.png', 'base.tex.png']
    return any(keyword in image_name.lower() for keyword in diffuse_keywords)

# Loop through all materials in the current scene
for mat in bpy.data.materials:
    if mat.use_nodes:
        # Get the nodes in the material's node tree
        nodes = mat.node_tree.nodes
        
        # Check if the material has a Principled BSDF node
        principled_node = None
        diffuse_image_texture = None
        
        for node in nodes:
            if node.type == 'BSDF_PRINCIPLED':
                principled_node = node
            if node.type == 'TEX_IMAGE':  # Look for any image texture
                image_texture = node
                
                # Check if the image is a diffuse texture based on naming pattern
                if image_texture.image and is_diffuse_image(image_texture.image.filepath):
                    diffuse_image_texture = image_texture
                    break  # Stop searching once we find the diffuse texture
        
        # Fallback to the first image texture if no diffuse texture is found
        if not diffuse_image_texture:
            for node in nodes:
                if node.type == 'TEX_IMAGE':
                    diffuse_image_texture = node
                    print(f"Warning: No diffuse texture found based on naming convention. Using first image")
                    break

        # If there's no Principled BSDF node, create one
        if not principled_node:
            principled_node = nodes.new(type='ShaderNodeBsdfPrincipled')
            principled_node.location = (0, 0)

        # If there's no diffuse Image Texture node, create one (this shouldn't happen if fallback is applied)
        if not diffuse_image_texture:
            diffuse_image_texture = nodes.new(type='ShaderNodeTexImage')
            diffuse_image_texture.location = (0, 0)
        
        # Create Texture Coordinate and Mapping nodes
        texture_coordinate_node = nodes.new(type='ShaderNodeTexCoord')
        texture_coordinate_node.location = (-200, 0)
        
        mapping_node = nodes.new(type='ShaderNodeMapping')
        mapping_node.location = (-100, 0)

        # Connect the Texture Coordinate to Mapping, and Mapping to Image Texture
        mat.node_tree.links.new(texture_coordinate_node.outputs['UV'], mapping_node.inputs['Vector'])
        mat.node_tree.links.new(mapping_node.outputs['Vector'], diffuse_image_texture.inputs['Vector'])
        
        # If there's a diffuse image texture, link it to the Principled BSDF
        if diffuse_image_texture:
            # Check if there are any existing links to the Base Color input
            base_color_input = principled_node.inputs['Base Color']
            if base_color_input.is_linked:
                # Clear any existing link to the Base Color input only if linked
                for link in base_color_input.links:
                    mat.node_tree.links.remove(link)
            
            # Link the image texture to the Principled BSDF's Base Color input
            mat.node_tree.links.new(diffuse_image_texture.outputs['Color'], principled_node.inputs['Base Color'])
        
        # Now, remove all unnecessary nodes (except the Principled BSDF and Image Texture)
        for node in nodes:
            if node != principled_node and node != diffuse_image_texture and node != texture_coordinate_node and node != mapping_node:
                if node.type != 'OUTPUT_MATERIAL':  # Don't remove the Material Output node
                    nodes.remove(node)

        # Ensure the Principled BSDF is connected to the Material Output
        material_output = nodes.get('Material Output')
        if material_output:
            if not material_output.inputs['Surface'].is_linked:
                mat.node_tree.links.new(principled_node.outputs['BSDF'], material_output.inputs['Surface'])
