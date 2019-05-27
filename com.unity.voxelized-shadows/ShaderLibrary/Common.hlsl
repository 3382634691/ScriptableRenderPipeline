#ifndef UNITY_VX_SHADOWMAPS_COMMON_INCLUDED
#define UNITY_VX_SHADOWMAPS_COMMON_INCLUDED

#if defined(SHADER_API_METAL)
#define USE_EMULATE_COUNTBITS
#endif

#define OFFSET_DIR 15
#define OFFSET_POINT 15 // TODO : define it for point light
#define OFFSET_SPOT 15 // TODO : define it for spot light

#define VX_SHADOWS_ONLY  0x80000000
#define VX_SHADOWS_BLEND 0x40000000

StructuredBuffer<uint> _VxShadowMapsBuffer;


uint emulateCLZ(uint x)
{
    // emulate it similar to count leading zero.
    // count leading 1bit.

    uint n = 32;
    uint y;

    y = x >> 16; if (y != 0) { n = n - 16; x = y; }
    y = x >>  8; if (y != 0) { n = n -  8; x = y; }
    y = x >>  4; if (y != 0) { n = n -  4; x = y; }
    y = x >>  2; if (y != 0) { n = n -  2; x = y; }
    y = x >>  1; if (y != 0) return n - 2;

    return n - x;
}

uint countBits(uint i)
{
#ifdef USE_EMULATE_COUNTBITS
    i = i - ((i >> 1) & 0x55555555);
    i = (i & 0x33333333) + ((i >> 2) & 0x33333333);

    return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
#else
    return countbits(i);
#endif
}

uint4 countBits(uint4 i)
{
#ifdef USE_EMULATE_COUNTBITS
    i = i - ((i >> 1) & 0x55555555);
    i = (i & 0x33333333) + ((i >> 2) & 0x33333333);

    return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
#else
    return countbits(i);
#endif
}

// todo : calculate uint2 and more?
uint CalculateRescale(uint srcPosbit, uint dstPosbit)
{
    return 32 - emulateCLZ(srcPosbit ^ dstPosbit);
}

void TraverseVxShadowMapPosQ(uint begin, uint typeOffset, uint3 posQ, out uint4 result)
{
    uint vxsmOffset = begin + typeOffset;
    uint dagScale = _VxShadowMapsBuffer[begin + 2];

    uint scale = dagScale - 1;

    // calculate where to go to child
    uint3 childDet = ((posQ >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint cellShift = childDet.x + childDet.y + childDet.z;
    uint cellbit   = 0x00000003 << cellShift;

    // initial access
    uint vxsmAccess = vxsmOffset;

    // calculate bit
    uint childmask = _VxShadowMapsBuffer[vxsmAccess] >> 16;
    uint shadowbit = (childmask & cellbit) >> cellShift;

    // determine whether it is intersected or not
    bool intersected = shadowbit == 0x00000003;

    uint mask = 0;
    uint childrenbit = 0;
    uint childIndex = 0;

    for (; scale > 3 && intersected; --scale)
    {
        // find next child node
        mask = ~(0xFFFFFFFF << cellShift);
        childrenbit = childmask & ((childmask & 0x0000AAAA) >> 1);
        childIndex = countBits(childrenbit & mask);

        // update access
        vxsmAccess = vxsmAccess + 1 + childIndex;
        vxsmAccess = vxsmOffset + _VxShadowMapsBuffer[vxsmAccess];

        // calculate where to go to child
        uint scaleShift = scale - 1;
        childDet  = ((posQ >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        cellShift = childDet.x + childDet.y + childDet.z;
        cellbit   = 0x00000003 << cellShift;

        // calculate bit
        childmask = _VxShadowMapsBuffer[vxsmAccess] >> 16;
        shadowbit = (childmask & cellbit) >> cellShift;

        // determine whether it is intersected or not
        intersected = shadowbit == 0x00000003;
    }

    uint nodeIndex = 0;

    // go further the rest of computation
    if (intersected)
    {
        mask = ~(0xFFFFFFFF << cellShift);
        childrenbit = childmask & ((childmask & 0x0000AAAA) >> 1);
        childIndex = countBits(childrenbit & mask);

        nodeIndex  = _VxShadowMapsBuffer[vxsmAccess + 1 + childIndex];
    }

    bool lit = shadowbit & 0x00000001;

#ifdef VX_SHADOWS_DEBUG
    result = uint4(nodeIndex, scale, lit, intersected);
#else
    result = uint4(nodeIndex, 0, lit, intersected);
#endif
}

void TraverseVxShadowMapPosQ2x2(uint begin, uint typeOffset, uint3 posQ_0, out uint4 results[4])
{
    uint vxsmOffset = begin + typeOffset;
    uint dagScale = _VxShadowMapsBuffer[begin + 2];

    uint3 posQ_1 = posQ_0 + uint3(1, 0, 0);
    uint3 posQ_2 = posQ_0 + uint3(0, 1, 0);
    uint3 posQ_3 = posQ_0 + uint3(1, 1, 0);

    uint scale = dagScale - 1;

    // calculate where to go to child
    uint3 childDet_0 = ((posQ_0 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_1 = ((posQ_1 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_2 = ((posQ_2 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_3 = ((posQ_3 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint4 cellShift4 = uint4(
        childDet_0.x + childDet_0.y + childDet_0.z,
        childDet_1.x + childDet_1.y + childDet_1.z,
        childDet_2.x + childDet_2.y + childDet_2.z,
        childDet_3.x + childDet_3.y + childDet_3.z);
    uint4 cellbit4 = 0x00000003 << cellShift4;

    // initial access
    uint4 vxsmAccess4 = vxsmOffset;

    // calculate bit
    uint4 childmask4 = uint4(
        _VxShadowMapsBuffer[vxsmAccess4.x],
        _VxShadowMapsBuffer[vxsmAccess4.y],
        _VxShadowMapsBuffer[vxsmAccess4.z],
        _VxShadowMapsBuffer[vxsmAccess4.w]) >> 16;
    uint4 shadowbit4 = (childmask4 & cellbit4) >> cellShift4;

    // determine whether it is intersected or not
    bool4 intersected4 = shadowbit4 == 0x00000003;

    uint4 mask4 = 0;
    uint4 childrenbit4 = 0;
    uint4 childIndex4 = 0;

    for (; scale > 3 && any(intersected4); --scale)
    {
        // find next child node
        mask4 = ~(0xFFFFFFFF << cellShift4);
        childrenbit4 = childmask4 & ((childmask4 & 0x0000AAAA) >> 1);
        childIndex4 = countBits(childrenbit4 & mask4);

        // update access
        vxsmAccess4 = vxsmAccess4 + 1 + childIndex4;
        vxsmAccess4 = vxsmOffset + uint4(
            _VxShadowMapsBuffer[vxsmAccess4.x],
            _VxShadowMapsBuffer[vxsmAccess4.y],
            _VxShadowMapsBuffer[vxsmAccess4.z],
            _VxShadowMapsBuffer[vxsmAccess4.w]);

        // calculate where to go to child
        uint scaleShift = scale - 1;
        childDet_0   = ((posQ_0 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_1   = ((posQ_1 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_2   = ((posQ_2 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_3   = ((posQ_3 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        cellShift4.x = dot(childDet_0, 1);
        cellShift4.y = dot(childDet_1, 1);
        cellShift4.z = dot(childDet_2, 1);
        cellShift4.w = dot(childDet_3, 1);
        cellbit4     = 0x00000003 << cellShift4;

        // calculate bit
        childmask4 = uint4(
            _VxShadowMapsBuffer[vxsmAccess4.x],
            _VxShadowMapsBuffer[vxsmAccess4.y],
            _VxShadowMapsBuffer[vxsmAccess4.z],
            _VxShadowMapsBuffer[vxsmAccess4.w]) >> 16;
        shadowbit4 = intersected4 ? (childmask4 & cellbit4) >> cellShift4 : shadowbit4;

        // determine whether it is intersected or not
        intersected4 = shadowbit4 == 0x00000003;
    }

    uint4 nodeIndex4 = 0;

    // go further the rest of computation
    if (any(intersected4))
    {
        mask4 = ~(0xFFFFFFFF << cellShift4);
        childrenbit4 = childmask4 & ((childmask4 & 0x0000AAAA) >> 1);
        childIndex4 = countBits(childrenbit4 & mask4);

        vxsmAccess4 = vxsmAccess4 + 1 + childIndex4;
        nodeIndex4 = uint4(
            _VxShadowMapsBuffer[vxsmAccess4.x],
            _VxShadowMapsBuffer[vxsmAccess4.y],
            _VxShadowMapsBuffer[vxsmAccess4.z],
            _VxShadowMapsBuffer[vxsmAccess4.w]);
    }

    bool4 lit4 = shadowbit4 & 0x00000001;

#ifdef VX_SHADOWS_DEBUG
    results[0] = uint4(nodeIndex4.x, 0, lit4.x, intersected4.x);
    results[1] = uint4(nodeIndex4.y, 0, lit4.y, intersected4.y);
    results[2] = uint4(nodeIndex4.z, 0, lit4.z, intersected4.z);
    results[3] = uint4(nodeIndex4.w, 0, lit4.w, intersected4.w);
#else
    results[0] = uint4(nodeIndex4.x, scale, lit4.x, intersected4.x);
    results[1] = uint4(nodeIndex4.y, scale, lit4.y, intersected4.y);
    results[2] = uint4(nodeIndex4.z, scale, lit4.z, intersected4.z);
    results[3] = uint4(nodeIndex4.w, scale, lit4.w, intersected4.w);
#endif
}

void TraverseVxShadowMapPosQ2x2x2(uint begin, uint typeOffset, uint3 posQ_0, out uint4 results[8])
{
    uint vxsmOffset = begin + typeOffset;
    uint dagScale = _VxShadowMapsBuffer[begin + 2];

    uint3 posQ_1 = posQ_0 + uint3(1, 0, 0);
    uint3 posQ_2 = posQ_0 + uint3(0, 1, 0);
    uint3 posQ_3 = posQ_0 + uint3(1, 1, 0);
    uint3 posQ_4 = posQ_0 + uint3(0, 0, 1);
    uint3 posQ_5 = posQ_0 + uint3(1, 0, 1);
    uint3 posQ_6 = posQ_0 + uint3(0, 1, 1);
    uint3 posQ_7 = posQ_0 + uint3(1, 1, 1);

    uint scale = dagScale - 1;

    // calculate where to go to child
    uint3 childDet_0 = ((posQ_0 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_1 = ((posQ_1 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_2 = ((posQ_2 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_3 = ((posQ_3 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_4 = ((posQ_4 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_5 = ((posQ_5 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_6 = ((posQ_6 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint3 childDet_7 = ((posQ_7 >> scale) & 0x00000001) << uint3(1, 2, 3);
    uint4 cellShift4_0 = uint4(
        childDet_0.x + childDet_0.y + childDet_0.z,
        childDet_1.x + childDet_1.y + childDet_1.z,
        childDet_2.x + childDet_2.y + childDet_2.z,
        childDet_3.x + childDet_3.y + childDet_3.z);
    uint4 cellShift4_1 = uint4(
        childDet_4.x + childDet_4.y + childDet_4.z,
        childDet_5.x + childDet_5.y + childDet_5.z,
        childDet_6.x + childDet_6.y + childDet_6.z,
        childDet_7.x + childDet_7.y + childDet_7.z);
    uint4 cellbit4_0 = 0x00000003 << cellShift4_0;
    uint4 cellbit4_1 = 0x00000003 << cellShift4_1;

    // initial access
    uint4 vxsmAccess4_0 = vxsmOffset;
    uint4 vxsmAccess4_1 = vxsmOffset;

    // calculate bit
    uint4 childmask4_0 = uint4(
        _VxShadowMapsBuffer[vxsmAccess4_0.x],
        _VxShadowMapsBuffer[vxsmAccess4_0.y],
        _VxShadowMapsBuffer[vxsmAccess4_0.z],
        _VxShadowMapsBuffer[vxsmAccess4_0.w]) >> 16;
    uint4 childmask4_1 = uint4(
        _VxShadowMapsBuffer[vxsmAccess4_1.x],
        _VxShadowMapsBuffer[vxsmAccess4_1.y],
        _VxShadowMapsBuffer[vxsmAccess4_1.z],
        _VxShadowMapsBuffer[vxsmAccess4_1.w]) >> 16;
    uint4 shadowbit4_0 = (childmask4_0 & cellbit4_0) >> cellShift4_0;
    uint4 shadowbit4_1 = (childmask4_1 & cellbit4_1) >> cellShift4_1;

    // determine whether it is intersected or not
    bool4 intersected4_0 = shadowbit4_0 == 0x00000003;
    bool4 intersected4_1 = shadowbit4_1 == 0x00000003;

    uint4 mask4_0 = 0;
    uint4 mask4_1 = 0;
    uint4 childrenbit4_0 = 0;
    uint4 childrenbit4_1 = 0;
    uint4 childIndex4_0 = 0;
    uint4 childIndex4_1 = 0;

    for (; scale > 3 && any(intersected4_0 || intersected4_1); --scale)
    {
        // find next child node
        mask4_0 = ~(0xFFFFFFFF << cellShift4_0);
        mask4_1 = ~(0xFFFFFFFF << cellShift4_1);
        childrenbit4_0 = childmask4_0 & ((childmask4_0 & 0x0000AAAA) >> 1);
        childrenbit4_1 = childmask4_1 & ((childmask4_1 & 0x0000AAAA) >> 1);
        childIndex4_0 = countBits(childrenbit4_0 & mask4_0);
        childIndex4_1 = countBits(childrenbit4_1 & mask4_1);

        // update access
        vxsmAccess4_0 = vxsmAccess4_0 + 1 + childIndex4_0;
        vxsmAccess4_1 = vxsmAccess4_1 + 1 + childIndex4_1;
        vxsmAccess4_0 = vxsmOffset + uint4(
            _VxShadowMapsBuffer[vxsmAccess4_0.x],
            _VxShadowMapsBuffer[vxsmAccess4_0.y],
            _VxShadowMapsBuffer[vxsmAccess4_0.z],
            _VxShadowMapsBuffer[vxsmAccess4_0.w]);
        vxsmAccess4_1 = vxsmOffset + uint4(
            _VxShadowMapsBuffer[vxsmAccess4_1.x],
            _VxShadowMapsBuffer[vxsmAccess4_1.y],
            _VxShadowMapsBuffer[vxsmAccess4_1.z],
            _VxShadowMapsBuffer[vxsmAccess4_1.w]);

        // calculate where to go to child
        uint scaleShift = scale - 1;
        childDet_0     = ((posQ_0 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_1     = ((posQ_1 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_2     = ((posQ_2 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_3     = ((posQ_3 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_4     = ((posQ_4 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_5     = ((posQ_5 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_6     = ((posQ_6 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        childDet_7     = ((posQ_7 >> scaleShift) & 0x00000001) << uint3(1, 2, 3);
        cellShift4_0.x = dot(childDet_0, 1);
        cellShift4_0.y = dot(childDet_1, 1);
        cellShift4_0.z = dot(childDet_2, 1);
        cellShift4_0.w = dot(childDet_3, 1);
        cellShift4_1.x = dot(childDet_4, 1);
        cellShift4_1.y = dot(childDet_5, 1);
        cellShift4_1.z = dot(childDet_6, 1);
        cellShift4_1.w = dot(childDet_7, 1);
        cellbit4_0     = 0x00000003 << cellShift4_0;
        cellbit4_1     = 0x00000003 << cellShift4_1;

        // calculate bit
        childmask4_0 = uint4(
            _VxShadowMapsBuffer[vxsmAccess4_0.x],
            _VxShadowMapsBuffer[vxsmAccess4_0.y],
            _VxShadowMapsBuffer[vxsmAccess4_0.z],
            _VxShadowMapsBuffer[vxsmAccess4_0.w]) >> 16;
        childmask4_1 = uint4(
            _VxShadowMapsBuffer[vxsmAccess4_1.x],
            _VxShadowMapsBuffer[vxsmAccess4_1.y],
            _VxShadowMapsBuffer[vxsmAccess4_1.z],
            _VxShadowMapsBuffer[vxsmAccess4_1.w]) >> 16;
        shadowbit4_0 = intersected4_0 ? (childmask4_0 & cellbit4_0) >> cellShift4_0 : shadowbit4_0;
        shadowbit4_1 = intersected4_1 ? (childmask4_1 & cellbit4_1) >> cellShift4_1 : shadowbit4_1;

        // determine whether it is intersected or not
        intersected4_0 = shadowbit4_0 == 0x00000003;
        intersected4_1 = shadowbit4_1 == 0x00000003;
    }

    uint4 nodeIndex4_0 = 0;
    uint4 nodeIndex4_1 = 0;

    // go further the rest of computation
    if (any(intersected4_0 || intersected4_1))
    {
        mask4_0 = ~(0xFFFFFFFF << cellShift4_0);
        mask4_1 = ~(0xFFFFFFFF << cellShift4_1);
        childrenbit4_0 = childmask4_0 & ((childmask4_0 & 0x0000AAAA) >> 1);
        childrenbit4_1 = childmask4_1 & ((childmask4_1 & 0x0000AAAA) >> 1);
        childIndex4_0 = countBits(childrenbit4_0 & mask4_0);
        childIndex4_1 = countBits(childrenbit4_1 & mask4_1);

        vxsmAccess4_0 = vxsmAccess4_0 + 1 + childIndex4_0;
        vxsmAccess4_1 = vxsmAccess4_1 + 1 + childIndex4_1;
        nodeIndex4_0 = uint4(
            _VxShadowMapsBuffer[vxsmAccess4_0.x],
            _VxShadowMapsBuffer[vxsmAccess4_0.y],
            _VxShadowMapsBuffer[vxsmAccess4_0.z],
            _VxShadowMapsBuffer[vxsmAccess4_0.w]);
        nodeIndex4_1 = uint4(
            _VxShadowMapsBuffer[vxsmAccess4_1.x],
            _VxShadowMapsBuffer[vxsmAccess4_1.y],
            _VxShadowMapsBuffer[vxsmAccess4_1.z],
            _VxShadowMapsBuffer[vxsmAccess4_1.w]);
    }

    bool4 lit4_0 = shadowbit4_0 & 0x00000001;
    bool4 lit4_1 = shadowbit4_1 & 0x00000001;

#ifdef VX_SHADOWS_DEBUG
    results[0] = uint4(nodeIndex4_0.x, 0, lit4_0.x, intersected4_0.x);
    results[1] = uint4(nodeIndex4_0.y, 0, lit4_0.y, intersected4_0.y);
    results[2] = uint4(nodeIndex4_0.z, 0, lit4_0.z, intersected4_0.z);
    results[3] = uint4(nodeIndex4_0.w, 0, lit4_0.w, intersected4_0.w);
    results[4] = uint4(nodeIndex4_1.x, 0, lit4_1.x, intersected4_1.x);
    results[5] = uint4(nodeIndex4_1.y, 0, lit4_1.y, intersected4_1.y);
    results[6] = uint4(nodeIndex4_1.z, 0, lit4_1.z, intersected4_1.z);
    results[7] = uint4(nodeIndex4_1.w, 0, lit4_1.w, intersected4_1.w);
#else
    results[0] = uint4(nodeIndex4_0.x, scale, lit4_0.x, intersected4_0.x);
    results[1] = uint4(nodeIndex4_0.y, scale, lit4_0.y, intersected4_0.y);
    results[2] = uint4(nodeIndex4_0.z, scale, lit4_0.z, intersected4_0.z);
    results[3] = uint4(nodeIndex4_0.w, scale, lit4_0.w, intersected4_0.w);
    results[4] = uint4(nodeIndex4_1.x, scale, lit4_1.x, intersected4_1.x);
    results[5] = uint4(nodeIndex4_1.y, scale, lit4_1.y, intersected4_1.y);
    results[6] = uint4(nodeIndex4_1.z, scale, lit4_1.z, intersected4_1.z);
    results[7] = uint4(nodeIndex4_1.w, scale, lit4_1.w, intersected4_1.w);
#endif
}

float TraverseNearestSampleVxShadowMap(uint begin, uint typeOffset, uint3 posQ, uint4 innerResult)
{
    uint vxsmOffset = begin + typeOffset;
    uint nodeIndex = innerResult.x;

    uint3 leaf = posQ % uint3(8, 8, 8);
    uint leafIndex = _VxShadowMapsBuffer[vxsmOffset + nodeIndex + leaf.z];
    if (leaf.y >= 4) leafIndex++;

    uint bitmask = _VxShadowMapsBuffer[vxsmOffset + leafIndex];

    uint maskShift = leaf.x + 8 * (leaf.y % 4);
    uint mask = 0x00000001 << maskShift;

    float attenuation = (bitmask & mask) == 0 ? 1.0 : 0.0;

    return attenuation;
}

float TraverseBilinearSampleVxShadowMap(uint begin, uint typeOffset, uint3 posQ_0, uint4 innerResults[4], float2 lerpWeight)
{
    uint vxsmOffset = begin + typeOffset;
    uint4 nodeIndex4 = vxsmOffset + uint4(
        innerResults[0].x,
        innerResults[1].x,
        innerResults[2].x,
        innerResults[3].x);

    uint3 posQ_1 = posQ_0 + uint3(1, 0, 0);
    uint3 posQ_2 = posQ_0 + uint3(0, 1, 0);
    uint3 posQ_3 = posQ_0 + uint3(1, 1, 0);

    uint4 leaf4_x = uint4(posQ_0.x, posQ_1.x, posQ_2.x, posQ_3.x) % 8;
    uint4 leaf4_y = uint4(posQ_0.y, posQ_1.y, posQ_2.y, posQ_3.y) % 8;
    uint4 leaf4_z = uint4(posQ_0.z, posQ_1.z, posQ_2.z, posQ_3.z) % 8;

    uint4 leafIndex = vxsmOffset + uint4(
        _VxShadowMapsBuffer[nodeIndex4.x + leaf4_z.x],
        _VxShadowMapsBuffer[nodeIndex4.y + leaf4_z.y],
        _VxShadowMapsBuffer[nodeIndex4.z + leaf4_z.z],
        _VxShadowMapsBuffer[nodeIndex4.w + leaf4_z.w]);
    leafIndex = leaf4_y < 4 ? leafIndex : (leafIndex + 1);

    uint4 bitmask4 = uint4(
        innerResults[0].z ? 0x00000000 : 0xFFFFFFFF,
        innerResults[1].z ? 0x00000000 : 0xFFFFFFFF,
        innerResults[2].z ? 0x00000000 : 0xFFFFFFFF,
        innerResults[3].z ? 0x00000000 : 0xFFFFFFFF);

    if (innerResults[0].w) bitmask4.x = _VxShadowMapsBuffer[leafIndex.x];
    if (innerResults[1].w) bitmask4.y = _VxShadowMapsBuffer[leafIndex.y];
    if (innerResults[2].w) bitmask4.z = _VxShadowMapsBuffer[leafIndex.z];
    if (innerResults[3].w) bitmask4.w = _VxShadowMapsBuffer[leafIndex.w];

    uint4 maskShift4 = mad(leaf4_y % 4, 8, leaf4_x);
    uint4 mask4 = uint4(1, 1, 1, 1) << maskShift4;

    float4 attenuation4 = (bitmask4 & mask4) == 0 ? 1.0 : 0.0;
    attenuation4.xy = lerp(attenuation4.xz, attenuation4.yw, lerpWeight.x);
    attenuation4.x  = lerp(attenuation4.x,  attenuation4.y,  lerpWeight.y);

    return attenuation4.x;
}

float TravereTrilinearSampleVxShadowMap(uint begin, uint typeOffset, uint3 posQ_0, uint4 innerResults[8], float3 lerpWeight)
{
    uint vxsmOffset = begin + typeOffset;
    uint4 nodeIndex4_0 = vxsmOffset + uint4(
        innerResults[0].x,
        innerResults[1].x,
        innerResults[2].x,
        innerResults[3].x);
    uint4 nodeIndex4_1 = vxsmOffset + uint4(
        innerResults[4].x,
        innerResults[5].x,
        innerResults[6].x,
        innerResults[7].x);

    uint3 posQ_1 = posQ_0 + uint3(1, 0, 0);
    uint3 posQ_2 = posQ_0 + uint3(0, 1, 0);
    uint3 posQ_3 = posQ_0 + uint3(1, 1, 0);
    uint3 posQ_4 = posQ_0 + uint3(0, 0, 1);
    uint3 posQ_5 = posQ_0 + uint3(1, 0, 1);
    uint3 posQ_6 = posQ_0 + uint3(0, 1, 1);
    uint3 posQ_7 = posQ_0 + uint3(1, 1, 1);

    uint4 leaf4_x0 = uint4(posQ_0.x, posQ_1.x, posQ_2.x, posQ_3.x) % 8;
    uint4 leaf4_y0 = uint4(posQ_0.y, posQ_1.y, posQ_2.y, posQ_3.y) % 8;
    uint4 leaf4_z0 = uint4(posQ_0.z, posQ_1.z, posQ_2.z, posQ_3.z) % 8;
    uint4 leaf4_x1 = uint4(posQ_4.x, posQ_5.x, posQ_6.x, posQ_7.x) % 8;
    uint4 leaf4_y1 = uint4(posQ_4.y, posQ_5.y, posQ_6.y, posQ_7.y) % 8;
    uint4 leaf4_z1 = uint4(posQ_4.z, posQ_5.z, posQ_6.z, posQ_7.z) % 8;

    uint4 leafIndex_0 = vxsmOffset + uint4(
        _VxShadowMapsBuffer[nodeIndex4_0.x + leaf4_z0.x],
        _VxShadowMapsBuffer[nodeIndex4_0.y + leaf4_z0.y],
        _VxShadowMapsBuffer[nodeIndex4_0.z + leaf4_z0.z],
        _VxShadowMapsBuffer[nodeIndex4_0.w + leaf4_z0.w]);
    uint4 leafIndex_1 = vxsmOffset + uint4(
        _VxShadowMapsBuffer[nodeIndex4_1.x + leaf4_z1.x],
        _VxShadowMapsBuffer[nodeIndex4_1.y + leaf4_z1.y],
        _VxShadowMapsBuffer[nodeIndex4_1.z + leaf4_z1.z],
        _VxShadowMapsBuffer[nodeIndex4_1.w + leaf4_z1.w]);
    leafIndex_0 = leaf4_y0 < 4 ? leafIndex_0 : (leafIndex_0 + 1);
    leafIndex_1 = leaf4_y1 < 4 ? leafIndex_1 : (leafIndex_1 + 1);

    uint4 bitmask4_0 = uint4(
        innerResults[0].z ? 0x00000000 : 0xFFFFFFFF,
        innerResults[1].z ? 0x00000000 : 0xFFFFFFFF,
        innerResults[2].z ? 0x00000000 : 0xFFFFFFFF,
        innerResults[3].z ? 0x00000000 : 0xFFFFFFFF);
    uint4 bitmask4_1 = uint4(
        innerResults[4].z ? 0x00000000 : 0xFFFFFFFF,
        innerResults[5].z ? 0x00000000 : 0xFFFFFFFF,
        innerResults[6].z ? 0x00000000 : 0xFFFFFFFF,
        innerResults[7].z ? 0x00000000 : 0xFFFFFFFF);

    if (innerResults[0].w) bitmask4_0.x = _VxShadowMapsBuffer[leafIndex_0.x];
    if (innerResults[1].w) bitmask4_0.y = _VxShadowMapsBuffer[leafIndex_0.y];
    if (innerResults[2].w) bitmask4_0.z = _VxShadowMapsBuffer[leafIndex_0.z];
    if (innerResults[3].w) bitmask4_0.w = _VxShadowMapsBuffer[leafIndex_0.w];
    if (innerResults[4].w) bitmask4_1.x = _VxShadowMapsBuffer[leafIndex_1.x];
    if (innerResults[5].w) bitmask4_1.y = _VxShadowMapsBuffer[leafIndex_1.y];
    if (innerResults[6].w) bitmask4_1.z = _VxShadowMapsBuffer[leafIndex_1.z];
    if (innerResults[7].w) bitmask4_1.w = _VxShadowMapsBuffer[leafIndex_1.w];

    uint4 maskShift4_0 = mad(leaf4_y0 % 4, 8, leaf4_x0);
    uint4 maskShift4_1 = mad(leaf4_y1 % 4, 8, leaf4_x1);
    uint4 mask4_0 = uint4(1, 1, 1, 1) << maskShift4_0;
    uint4 mask4_1 = uint4(1, 1, 1, 1) << maskShift4_1;

    float4 attenuation4_0 = (bitmask4_0 & mask4_0) == 0 ? 1.0 : 0.0;
    float4 attenuation4_1 = (bitmask4_1 & mask4_1) == 0 ? 1.0 : 0.0;
    attenuation4_0.xy = lerp(attenuation4_0.xz, attenuation4_0.yw, lerpWeight.x);
    attenuation4_0.x  = lerp(attenuation4_0.x,  attenuation4_0.y,  lerpWeight.y);
    attenuation4_1.xy = lerp(attenuation4_1.xz, attenuation4_1.yw, lerpWeight.x);
    attenuation4_1.x  = lerp(attenuation4_1.x,  attenuation4_1.y,  lerpWeight.y);

    return lerp(attenuation4_0.x, attenuation4_1.x, lerpWeight.z);
}

uint MaskBitsetVxShadowsType(uint vxShadowsBitset)
{
    return vxShadowsBitset & 0xC0000000;
}

uint MaskBitsetVxShadowMapBegin(uint vxShadowsBitset)
{
    return vxShadowsBitset & 0x3FFFFFFF;
}

bool IsVxShadowsEnabled(uint vxShadowsBitset)
{
    return MaskBitsetVxShadowsType(vxShadowsBitset) != 0x00000000;
}

bool IsVxShadowsDisabled(uint vxShadowsBitset)
{
    return MaskBitsetVxShadowsType(vxShadowsBitset) == 0x00000000;
}

bool IsVxShadowsOnly(uint vxShadowsBitset)
{
    return MaskBitsetVxShadowsType(vxShadowsBitset) == VX_SHADOWS_ONLY;
}

float NearestSampleVxShadowing(uint begin, float3 positionWS)
{
    uint dagScale = _VxShadowMapsBuffer[begin + 2];
    uint voxelResolution = 1 << dagScale;
    float4x4 worldToShadowMatrix =
    {
        asfloat(_VxShadowMapsBuffer[begin +  3]),
        asfloat(_VxShadowMapsBuffer[begin +  4]),
        asfloat(_VxShadowMapsBuffer[begin +  5]),
        asfloat(_VxShadowMapsBuffer[begin +  6]),

        asfloat(_VxShadowMapsBuffer[begin +  7]),
        asfloat(_VxShadowMapsBuffer[begin +  8]),
        asfloat(_VxShadowMapsBuffer[begin +  9]),
        asfloat(_VxShadowMapsBuffer[begin + 10]),

        asfloat(_VxShadowMapsBuffer[begin + 11]),
        asfloat(_VxShadowMapsBuffer[begin + 12]),
        asfloat(_VxShadowMapsBuffer[begin + 13]),
        asfloat(_VxShadowMapsBuffer[begin + 14]),

        0.0, 0.0, 0.0, 1.0,
    };

    float3 posNDC = mul(worldToShadowMatrix, float4(positionWS, 1.0)).xyz;
    float3 posP = posNDC * (float)voxelResolution;
    uint3  posQ = (uint3)posP;

    if (any(posQ >= (voxelResolution.xxx - 1)))
        return 1;

    uint4 result;
    TraverseVxShadowMapPosQ(begin, OFFSET_DIR, posQ, result);

    if (result.w == 0)
        return result.z ? 1 : 0;

    float attenuation = TraverseNearestSampleVxShadowMap(begin, OFFSET_DIR, posQ, result);

    return attenuation;
}

#endif // UNITY_VX_SHADOWMAPS_COMMON_INCLUDED
