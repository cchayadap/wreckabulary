import json
import struct

GLB_MAGIC = 0x46546C67
CHUNK_JSON = 0x4E4F534A
CHUNK_BIN = 0x004E4942


class Glb:
    def __init__(self, path):
        self.path = path
        with open(path, "rb") as f:
            data = f.read()
        magic, version, _length = struct.unpack_from("<III", data, 0)
        if magic != GLB_MAGIC or version != 2:
            raise ValueError(f"{path}: not a glTF 2.0 binary")
        self.json = None
        self.bin = b""
        offset = 12
        while offset < len(data):
            chunk_len, chunk_type = struct.unpack_from("<II", data, offset)
            body = data[offset + 8: offset + 8 + chunk_len]
            if chunk_type == CHUNK_JSON:
                self.json = json.loads(body.decode("utf-8"))
            elif chunk_type == CHUNK_BIN:
                self.bin = body
            offset += 8 + chunk_len
        if self.json is None:
            raise ValueError(f"{path}: no JSON chunk")

    def image_bytes(self, image_index):
        img = self.json["images"][image_index]
        view = self.json["bufferViews"][img["bufferView"]]
        start = view.get("byteOffset", 0)
        return self.bin[start: start + view["byteLength"]]

    def image_name(self, image_index):
        img = self.json["images"][image_index]
        return img.get("name") or f"image_{image_index}"

    def texture_image(self, texture_info):
        if not texture_info:
            return None
        tex = self.json["textures"][texture_info["index"]]
        return tex.get("source")

    def materials(self):
        for mat in self.json.get("materials", []):
            pbr = mat.get("pbrMetallicRoughness", {})
            yield {
                "name": mat.get("name", "unnamed"),
                "baseColor": pbr.get("baseColorFactor", [1, 1, 1, 1]),
                "metallic": pbr.get("metallicFactor", 1.0),
                "roughness": pbr.get("roughnessFactor", 1.0),
                "emissive": mat.get("emissiveFactor", [0, 0, 0]),
                "alphaMode": mat.get("alphaMode", "OPAQUE"),
                "alphaCutoff": mat.get("alphaCutoff", 0.5),
                "doubleSided": mat.get("doubleSided", False),
                "_baseImage": self.texture_image(pbr.get("baseColorTexture")),
                "_normalImage": self.texture_image(mat.get("normalTexture")),
                "_emissiveImage": self.texture_image(mat.get("emissiveTexture")),
            }

    def node_by_name(self, name):
        for node in self.json.get("nodes", []):
            if node.get("name") == name:
                return node
        return None

    def mesh_nodes(self):
        return [n for n in self.json.get("nodes", []) if "mesh" in n]
