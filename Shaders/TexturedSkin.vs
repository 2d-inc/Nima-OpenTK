attribute vec2 VertexPosition;
attribute vec2 VertexTexCoord;
attribute vec4 VertexBoneIndices;
attribute vec4 VertexWeights;

uniform mat4 ProjectionMatrix;
uniform mat4 WorldMatrix;
uniform mat4 ViewMatrix;
// Currently support a max of 41 bones.
uniform vec3 BoneMatrices[82];
varying vec2 TexCoord;

/*// when looping construct the matrix as follow (other values are from identity)
_Transform[0] = transform[0]; m[0]
_Transform[1] = transform[1]; m[1]
_Transform[4] = transform[2]; m[2]
_Transform[5] = transform[3]; n[0]
_Transform[12] = transform[4]; n[1]
_Transform[13] = transform[5]; n[2]

mat4(
  vec4,           //first column
  vec4,           //second column
  vec4,           //third column
  vec4);          //fourth column

mat4 xform = mat4(	1.0, 0.0, 0.0, 0.0,
    						0.0, 1.0, 0.0, 0.0,
    						0.0, 0.0, 1.0, 0.0,
    						0.0, 0.0, 0.0, 1.0);
  */

void main(void)
{
	TexCoord = VertexTexCoord;

	vec2 position = vec2(0.0, 0.0);

	vec4 p = WorldMatrix * vec4(VertexPosition.x, VertexPosition.y, 0.0, 1.0);
	float x = p[0];
	float y = p[1];

	for(int i = 0; i < 4; i++)
	{
		float weight = VertexWeights[i];

		int matrixIndex = int(VertexBoneIndices[i])*2;
		vec3 m = BoneMatrices[matrixIndex];
		vec3 n = BoneMatrices[matrixIndex+1];

		position[0] += (m[0] * x + m[2] * y + n[1]) * weight;
		position[1] += (m[1] * x + n[0] * y + n[2]) * weight;
    }
    vec4 pos = ViewMatrix * vec4(position.x, position.y, 0.0, 1.0);
    gl_Position = ProjectionMatrix * vec4(pos.xyz, 1.0);
}