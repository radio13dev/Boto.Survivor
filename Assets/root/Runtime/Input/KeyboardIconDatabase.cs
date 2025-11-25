using UnityEngine;

[CreateAssetMenu(fileName = "KeyboardIconDatabse", menuName = "ScriptableObjects/KeyboardIconDatabase")]
public class KeyboardIconDatabase : ScriptableObject
{
    [Header("Letters")]
    public Sprite a;
    public Sprite b;
    public Sprite c;
    public Sprite d;
    public Sprite e;
    public Sprite f;
    public Sprite g;
    public Sprite h;
    public Sprite i;
    public Sprite j;
    public Sprite k;
    public Sprite l;
    public Sprite m;
    public Sprite n;
    public Sprite o;
    public Sprite p;
    public Sprite q;
    public Sprite r;
    public Sprite s;
    public Sprite t;
    public Sprite u;
    public Sprite v;
    public Sprite w;
    public Sprite x;
    public Sprite y;
    public Sprite z;

    [Header("Numbers")]
    public Sprite one;
    public Sprite two;
    public Sprite three;
    public Sprite four;
    public Sprite five;
    public Sprite six;
    public Sprite seven;
    public Sprite eight;
    public Sprite nine;
    public Sprite zero;

    [Header("Arrows")]
    public Sprite upArrow;
    public Sprite downArrow;
    public Sprite leftArrow;
    public Sprite rightArrow;

    [Header("Functional")]
    public Sprite space;
    public Sprite enter;
    public Sprite escape;
    public Sprite backspace;
    public Sprite tab;
    public Sprite capsLock;
    public Sprite tilde;

    [Header("Modifiers")]
    public Sprite leftShift;
    public Sprite rightShift;
    public Sprite leftCtrl;
    public Sprite rightCtrl;
    public Sprite leftAlt;
    public Sprite rightAlt;

    public Sprite GetSprite(string bindingPath)
    {
        // Input System paths are usually lowercase key names
        switch (bindingPath)
        {
            // Letters
            case "<Keyboard>/a": return a;
            case "<Keyboard>/b": return b;
            case "<Keyboard>/c": return c;
            case "<Keyboard>/d": return d;
            case "<Keyboard>/e": return e;
            case "<Keyboard>/f": return f;
            case "<Keyboard>/g": return g;
            case "<Keyboard>/h": return h;
            case "<Keyboard>/i": return i;
            case "<Keyboard>/j": return j;
            case "<Keyboard>/k": return k;
            case "<Keyboard>/l": return l;
            case "<Keyboard>/m": return m;
            case "<Keyboard>/n": return n;
            case "<Keyboard>/o": return o;
            case "<Keyboard>/p": return p;
            case "<Keyboard>/q": return q;
            case "<Keyboard>/r": return r;
            case "<Keyboard>/s": return s;
            case "<Keyboard>/t": return t;
            case "<Keyboard>/u": return u;
            case "<Keyboard>/v": return v;
            case "<Keyboard>/w": return w;
            case "<Keyboard>/x": return x;
            case "<Keyboard>/y": return y;
            case "<Keyboard>/z": return z;

            // Numbers (Top row)
            case "<Keyboard>/1": return one;
            case "<Keyboard>/2": return two;
            case "<Keyboard>/3": return three;
            case "<Keyboard>/4": return four;
            case "<Keyboard>/5": return five;
            case "<Keyboard>/6": return six;
            case "<Keyboard>/7": return seven;
            case "<Keyboard>/8": return eight;
            case "<Keyboard>/9": return nine;
            case "<Keyboard>/0": return zero;

            // Arrows
            case "<Keyboard>/upArrow": return upArrow;
            case "<Keyboard>/downArrow": return downArrow;
            case "<Keyboard>/leftArrow": return leftArrow;
            case "<Keyboard>/rightArrow": return rightArrow;

            // Functional
            case "<Keyboard>/space": return space;
            case "<Keyboard>/enter": return enter;
            case "<Keyboard>/escape": return escape;
            case "<Keyboard>/backspace": return backspace;
            case "<Keyboard>/tab": return tab;
            case "<Keyboard>/capsLock": return capsLock;

            // Modifiers
            case "<Keyboard>/leftShift": return leftShift;
            case "<Keyboard>/rightShift": return rightShift;
            case "<Keyboard>/leftCtrl": return leftCtrl;
            case "<Keyboard>/rightCtrl": return rightCtrl;
            case "<Keyboard>/leftAlt": return leftAlt;
            case "<Keyboard>/rightAlt": return rightAlt;
            case "<Keyboard>/shift": return leftShift;
            case "<Keyboard>/ctrl": return leftCtrl;
            case "<Keyboard>/alt": return leftAlt;
        }
        return null;
    }
}