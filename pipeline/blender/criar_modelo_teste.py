"""
Cria um esqueleto guerreiro simples, com armadura e 4 animações, para testar o pipeline
sem precisar baixar nenhum modelo.

Uso:
    blender -b --python blender/criar_modelo_teste.py -- modelos/teste
Gera: modelos/teste/esqueleto_teste.blend e modelos/teste/esqueleto_teste.fbx
"""

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector, Matrix


def args():
    a = sys.argv
    a = a[a.index("--") + 1:] if "--" in a else a[1:]
    return a[0] if a else "modelos/teste"


def material(nome, cor, emissao=0.0):
    m = bpy.data.materials.new(nome)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*cor, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.8
    if emissao:
        bsdf.inputs["Emission Color"].default_value = (*cor, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emissao
    return m


def peca(nome, tipo, local, escala, osso, mat, rot=(0, 0, 0)):
    malha = bpy.data.meshes.new(nome)
    bm = bmesh.new()
    if tipo == "cubo":
        bmesh.ops.create_cube(bm, size=1.0)
    elif tipo == "esfera":
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.5)
    elif tipo == "cilindro":
        bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.5, radius2=0.5, depth=1.0)
    m = Matrix.Translation(local) @ Matrix.Rotation(rot[0], 4, "X") @ Matrix.Rotation(rot[1], 4, "Y") \
        @ Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Diagonal((*escala, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    bm.to_mesh(malha)
    bm.free()
    malha.materials.append(mat)
    obj = bpy.data.objects.new(nome, malha)
    bpy.context.scene.collection.objects.link(obj)
    grupo = obj.vertex_groups.new(name=osso)
    grupo.add(list(range(len(malha.vertices))), 1.0, "REPLACE")
    return obj


def main():
    saida = os.path.abspath(args())
    os.makedirs(saida, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cena = bpy.context.scene

    # --- armadura (personagem olhando para -Y) ---
    dados = bpy.data.armatures.new("Esqueleto")
    arm = bpy.data.objects.new("Esqueleto", dados)
    cena.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    ossos = {
        "quadril": ((0, 0, 0.95), (0, 0, 1.10), None),
        "coluna": ((0, 0, 1.10), (0, 0, 1.45), "quadril"),
        "cabeca": ((0, 0, 1.45), (0, 0, 1.75), "coluna"),
        "braco.L": ((0.24, 0, 1.42), (0.24, 0, 1.02), "coluna"),
        "braco.R": ((-0.24, 0, 1.42), (-0.24, 0, 1.02), "coluna"),
        "perna.L": ((0.10, 0, 0.95), (0.10, 0, 0.05), "quadril"),
        "perna.R": ((-0.10, 0, 0.95), (-0.10, 0, 0.05), "quadril"),
    }
    for nome, (cab, cau, pai) in ossos.items():
        b = dados.edit_bones.new(nome)
        b.head, b.tail = Vector(cab), Vector(cau)
        if pai:
            b.parent = dados.edit_bones[pai]
    bpy.ops.object.mode_set(mode="OBJECT")

    osso = material("Osso", (0.85, 0.80, 0.68))
    ferro = material("Ferro", (0.12, 0.12, 0.15))
    ferrugem = material("Lamina", (0.45, 0.22, 0.10))
    capa = material("Capa", (0.35, 0.03, 0.05))
    olho = material("Olho", (0.4, 1.0, 0.5), emissao=8.0)

    partes = [
        peca("Pelvis", "cubo", (0, 0, 1.0), (0.28, 0.16, 0.14), "quadril", osso),
        peca("Costelas", "cubo", (0, 0, 1.28), (0.34, 0.20, 0.30), "coluna", osso),
        peca("Peitoral", "cubo", (0, -0.04, 1.32), (0.38, 0.16, 0.22), "coluna", ferro),
        peca("Capa", "cubo", (0, 0.13, 1.05), (0.40, 0.04, 0.70), "coluna", capa),
        peca("Cranio", "esfera", (0, 0, 1.62), (0.26, 0.26, 0.28), "cabeca", osso),
        peca("Elmo", "cilindro", (0, 0, 1.72), (0.28, 0.28, 0.12), "cabeca", ferro),
        peca("Olho.L", "esfera", (0.06, -0.12, 1.63), (0.06, 0.04, 0.05), "cabeca", olho),
        peca("Olho.R", "esfera", (-0.06, -0.12, 1.63), (0.06, 0.04, 0.05), "cabeca", olho),
        peca("Braco.L", "cilindro", (0.24, 0, 1.22), (0.08, 0.08, 0.42), "braco.L", osso),
        peca("Braco.R", "cilindro", (-0.24, 0, 1.22), (0.08, 0.08, 0.42), "braco.R", osso),
        peca("Escudo", "cilindro", (0.31, -0.02, 1.15), (0.36, 0.36, 0.05), "braco.L", ferro,
             rot=(0, math.radians(90), 0)),
        peca("Espada", "cubo", (-0.24, -0.40, 1.02), (0.05, 0.75, 0.08), "braco.R", ferrugem),
        peca("Guarda", "cubo", (-0.24, -0.05, 1.02), (0.22, 0.05, 0.05), "braco.R", ferro),
        peca("Perna.L", "cilindro", (0.10, 0, 0.50), (0.09, 0.09, 0.90), "perna.L", osso),
        peca("Perna.R", "cilindro", (-0.10, 0, 0.50), (0.09, 0.09, 0.90), "perna.R", osso),
    ]

    bpy.ops.object.select_all(action="DESELECT")
    for p in partes:
        p.select_set(True)
    bpy.context.view_layer.objects.active = partes[0]
    bpy.ops.object.join()
    corpo = bpy.context.view_layer.objects.active
    corpo.name = "Corpo"
    corpo.parent = arm
    mod = corpo.modifiers.new("Armadura", "ARMATURE")
    mod.object = arm

    # --- animações ---
    arm.animation_data_create()
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"

    def animar(nome, chaves):
        """chaves: {frame: {osso: (rx, ry, rz) em graus, "osso@loc": (x, y, z)}}"""
        acao = bpy.data.actions.new(nome)
        acao.use_fake_user = True
        arm.animation_data.action = acao
        for pb in arm.pose.bones:
            pb.rotation_euler = (0, 0, 0)
            pb.location = (0, 0, 0)
        for f, poses in sorted(chaves.items()):
            for pb in arm.pose.bones:
                pb.rotation_euler = (0, 0, 0)
                pb.location = (0, 0, 0)
            for chave, valor in poses.items():
                if chave.endswith("@loc"):
                    arm.pose.bones[chave[:-4]].location = valor
                else:
                    arm.pose.bones[chave].rotation_euler = tuple(math.radians(v) for v in valor)
            for pb in arm.pose.bones:
                pb.keyframe_insert("rotation_euler", frame=f)
                pb.keyframe_insert("location", frame=f)
        return acao

    animar("Parado", {
        1: {"coluna": (2, 0, 0), "cabeca": (-3, 0, 0), "braco.L": (5, 0, 0), "braco.R": (-5, 0, 0)},
        13: {"coluna": (-2, 0, 0), "cabeca": (4, 0, 0), "braco.L": (-5, 0, 0), "braco.R": (5, 0, 0),
             "quadril@loc": (0, -0.03, 0)},
        25: {"coluna": (2, 0, 0), "cabeca": (-3, 0, 0), "braco.L": (5, 0, 0), "braco.R": (-5, 0, 0)},
    })
    animar("Andar", {
        1: {"perna.L": (30, 0, 0), "perna.R": (-30, 0, 0), "braco.L": (-25, 0, 0), "braco.R": (25, 0, 0)},
        7: {"quadril@loc": (0, 0.05, 0)},
        13: {"perna.L": (-30, 0, 0), "perna.R": (30, 0, 0), "braco.L": (25, 0, 0), "braco.R": (-25, 0, 0)},
        19: {"quadril@loc": (0, 0.05, 0)},
        25: {"perna.L": (30, 0, 0), "perna.R": (-30, 0, 0), "braco.L": (-25, 0, 0), "braco.R": (25, 0, 0)},
    })
    animar("Atacar", {
        1: {},
        8: {"braco.R": (-140, 0, 0), "coluna": (-10, 0, -20)},
        12: {"braco.R": (60, 0, 0), "coluna": (15, 0, 25)},
        20: {},
    })
    animar("Morrer", {
        1: {},
        6: {"coluna": (25, 0, 0), "cabeca": (25, 0, 0), "braco.L": (35, 0, 0), "braco.R": (35, 0, 0),
            "perna.L": (10, 0, 0), "perna.R": (-5, 0, 0)},
        16: {"quadril": (-60, 0, 0), "quadril@loc": (0, -0.45, 0), "coluna": (10, 0, 0),
             "cabeca": (-25, 0, 0), "braco.L": (-50, 0, 25), "braco.R": (-50, 0, -25),
             "perna.L": (15, 0, 0), "perna.R": (5, 0, 0)},
        30: {"quadril": (-90, 0, 0), "quadril@loc": (0, -0.82, 0), "cabeca": (-15, 0, 25),
             "braco.L": (-80, 0, 45), "braco.R": (-70, 0, -40), "perna.L": (8, 0, 6), "perna.R": (0, 0, -6)},
    })
    arm.animation_data.action = bpy.data.actions["Parado"]

    blend = os.path.join(saida, "esqueleto_teste.blend")
    fbx = os.path.join(saida, "esqueleto_teste.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    bpy.ops.export_scene.fbx(filepath=fbx, bake_anim=True, bake_anim_use_all_actions=True,
                             bake_anim_use_nla_strips=False, add_leaf_bones=False)
    print(f"[necro-pipeline] Modelo de teste salvo em {blend} e {fbx}")


if __name__ == "__main__":
    main()
