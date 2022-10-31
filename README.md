# GPU Accelerated Diamond-Square

![](Media/terrain.png)

**See the article: https://www.vertexfragment.com/ramblings/diamond-square/**

This repository presents a GPU accelerated implementation of the classic Diamond-Square algorithm, alongside a multi-threaded C# approach. As shown in the table below, the GPU version is 17x faster than the standard single-threaded approach and 4x faster than the multi-threaded implementation which runs a worker for every 128x128 chunk.

All timings are in milliseconds.

| Implementation     | 512 x 512 | 1024 x 1024 | 2048 x 2048 | 4096 x 4096 | 8192 x 8192 |
| ------------------ | --------- | ----------- | ----------- | ----------- | ----------- |
| Single-threaded C# | 38.3      | 161.6       | 740.2       | 2987.3      | 13042.1     |
| Multi-threaded C#  | 26.7      | 67.9        | 224.4       | 823.9       | 3121.5      |
| GPU-Accelerated    | 2.1       | 9.3         | 41.7        | 211.6       | 778.2       |

_Benchmarked on an Intel i7-4790K and NVIDIA GeForce GTX 980 Ti._

## Requirements

Unity v2022.1.11f or later to run the demo.

The multi-threaded implementation (`DiamondSquare.cs`) can be used outside of Unity, however the GPU implementation (`DiamondSquareGPU.cs`) uses the Unity Compute Shader API but the shader itself can be plugged into other engines.