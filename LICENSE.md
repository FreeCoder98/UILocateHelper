一、背景及功能
在Unity项目中，开发与重构每天都要和场景打交道，常见的问题有

1、在Hierarchy面板上找到某个UI元素对应的节点，需要不停点击Scene面板里UI的位置，
Unity使用二分查找来定位UI元素，当UI元素特别多的时候需要点击很多次，容易误操作。

2、查找脚本或UI元素被引用的情况，比如想知道某个Button的控制逻辑，需要先找到引用Button的脚本。

基于此背景开发Unity小旋风，他的功能包含：
1、在Scene面板上，移动鼠标指针到需要定位的UI元素上并右键，能快速UI元素在Hierarchy面板上的定位。
2、在Scene面板上，移动鼠标指针到UI元素上并右键，显示这个GameObject上挂的脚本列表，点击想要查找引用的脚本即可在Unity中定位到。

二、简要逻辑
定位UI元素
1、获取输入
获取Scene面板上所有的GameObject

2、节点过滤（遍历输入）
GameObject的RectTransform是否包含鼠标所在的坐标
GameObject的深度是否在指定的深度范围内（根据项目设置）
GameObejct的名称过滤（根据项目设置）
GameObejct上挂载了某些特殊脚本的（根据项目设置）

3、优先级排序
GameObject的名称（含有btn,img,txt的等可视UI元素优先级高）
GameObject的RectTranfrom范围大小（范围越大优先级越低）
GameObject的深度 （深度越大优先级越高）

查找引用（脚本 and 节点）
1、获取输入
指定GameObject的InstanceID

2、数据处理
找到该GameObject所在的根节点
获取该根节点所有子节点的Component
遍历Component，反射获取字段的实例值
该实例转换成GameObject获取InstanceID进行对比
