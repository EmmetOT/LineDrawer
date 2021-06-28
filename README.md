# LineDrawer
A simple tool for drawing anti-aliased lines in Unity, similar to Handles.DrawAAPolyLine but not a gizmo. 

I made this because I wanted to reproduce the functionality of Handles.DrawAAPolyLine but not just in Gizmos. Unity's LineRenderer is not really any good to me: I find it cumbersome and it doesn't render at a consistent width in screen space. 

This tool has a very straightforward API, you can add, remove, or adjust points at will. Each point has its own position, width, and colour, with the values interpolated between them. This is achieved using signed distances fields and a Graphics.DrawMesh call. It should work just fine with instancing, too.

![lineDrawer](https://user-images.githubusercontent.com/18707147/123711084-c697cb80-d867-11eb-939c-06056db5f1ff.gif)

![gravity1](https://user-images.githubusercontent.com/18707147/123711122-d6afab00-d867-11eb-8b6f-002dc0c94c61.gif)
