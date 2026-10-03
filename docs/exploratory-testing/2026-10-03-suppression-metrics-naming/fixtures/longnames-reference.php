<?php
class LongNames {
    public function m($items) {
        foreach ($items as $thisIsAVeryLongForeachVariableName) { echo $thisIsAVeryLongForeachVariableName; }
        $f = fn($thisIsAVeryLongLambdaParameterName) => $thisIsAVeryLongLambdaParameterName;
        return $f;
    }
}
